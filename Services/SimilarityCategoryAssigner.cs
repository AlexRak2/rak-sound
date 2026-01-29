using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SonnissBrowser
{
    /// <summary>
    /// Second-pass categorizer:
    /// - Build TF-IDF vectors for "known good" categorized items
    /// - For unknown/low-confidence items, find nearest neighbors via cosine similarity
    /// - Assign the winning category if similarity >= threshold
    /// 
    /// Designed to be FAST on large libraries using an inverted index (token -> candidates).
    /// </summary>
    public sealed class SimilarityCategoryAssigner
    {
        private static readonly Regex Splitter =
            new(@"[^a-z0-9]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Keep this small; sound libs have tons of junk tokens.
        private static readonly HashSet<string> Stop =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "a","an","and","or","the","to","of","in","on","for","with","at","by","from",
                "mono","stereo","loop","loops","one","shot","oneshot","shot","take",
                "v1","v2","v3","v4","v5","final","edit","mix","master",
                "dry","wet","close","far","dist","distant","near","room","mic","mics",
                "sfx","fx","wav","mp3","flac","ogg","aiff","aif","aac","m4a"
            };

        // If a token is mostly digits or tiny, skip it.
        private static bool IsBadToken(string t)
        {
            if (t.Length <= 2) return true;
            int digits = 0;
            for (int i = 0; i < t.Length; i++)
                if (char.IsDigit(t[i])) digits++;
            return digits >= t.Length - 1;
        }

        private static List<string> Tokenize(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<string>();
            var parts = Splitter.Split(text.ToLowerInvariant());

            var tokens = new List<string>(parts.Length);
            foreach (var p in parts)
            {
                var t = p.Trim();
                if (t.Length == 0) continue;
                if (Stop.Contains(t)) continue;
                if (IsBadToken(t)) continue;
                tokens.Add(t);
            }
            return tokens;
        }

        private sealed class Vec
        {
            public Dictionary<string, float> W = new(StringComparer.OrdinalIgnoreCase);
            public float Norm;
        }

        // Built state
        private readonly Dictionary<string, float> _idf = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<Vec> _knownVecs = new();
        private readonly List<string> _knownCats = new();
        private readonly Dictionary<string, List<int>> _inv = new(StringComparer.OrdinalIgnoreCase);

        private readonly int _topK;
        private readonly float _minSimilarity;

        public SimilarityCategoryAssigner(int topK = 7, float minSimilarity = 0.22f)
        {
            _topK = Math.Max(1, topK);
            _minSimilarity = minSimilarity;
        }

        /// <summary>
        /// Build index from known-good categorized items.
        /// </summary>
        public void BuildIndex(IEnumerable<(string text, string category)> known)
        {
            _idf.Clear();
            _knownVecs.Clear();
            _knownCats.Clear();
            _inv.Clear();

            // 1) Collect tokenized docs and DF
            var docs = new List<List<string>>();
            var cats = new List<string>();

            var df = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var (text, category) in known)
            {
                var toks = Tokenize(text);
                if (toks.Count == 0) continue;

                docs.Add(toks);
                cats.Add(category);

                foreach (var tok in toks.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    df.TryGetValue(tok, out int c);
                    df[tok] = c + 1;
                }
            }

            int N = docs.Count;
            if (N == 0) return;

            // 2) Compute IDF
            // Smooth IDF: log((N+1)/(df+1)) + 1
            foreach (var kv in df)
            {
                var idf = (float)(Math.Log((N + 1.0) / (kv.Value + 1.0)) + 1.0);
                _idf[kv.Key] = idf;
            }

            // 3) Build known vectors + inverted index
            for (int i = 0; i < docs.Count; i++)
            {
                var vec = ToTfidf(docs[i]);
                _knownVecs.Add(vec);
                _knownCats.Add(cats[i]);

                foreach (var tok in vec.W.Keys)
                {
                    if (!_inv.TryGetValue(tok, out var list))
                    {
                        list = new List<int>(8);
                        _inv[tok] = list;
                    }
                    list.Add(i);
                }
            }
        }

        private Vec ToTfidf(List<string> tokens)
        {
            var tf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in tokens)
            {
                tf.TryGetValue(t, out int c);
                tf[t] = c + 1;
            }

            var v = new Vec();
            float sumSq = 0f;

            foreach (var kv in tf)
            {
                if (!_idf.TryGetValue(kv.Key, out var idf)) continue;

                // Sublinear TF helps a bit on repeated words
                float w = (float)(1.0 + Math.Log(kv.Value)) * idf;
                v.W[kv.Key] = w;
                sumSq += w * w;
            }

            v.Norm = (float)Math.Sqrt(sumSq);
            if (v.Norm < 1e-6f) v.Norm = 1e-6f;

            return v;
        }

        private float Cosine(in Vec a, in Vec b)
        {
            // dot / (|a||b|)
            // iterate smaller map
            var small = a.W.Count <= b.W.Count ? a.W : b.W;
            var large = ReferenceEquals(small, a.W) ? b.W : a.W;

            float dot = 0f;
            foreach (var kv in small)
            {
                if (large.TryGetValue(kv.Key, out var w2))
                    dot += kv.Value * w2;
            }
            return dot / (a.Norm * b.Norm);
        }

        /// <summary>
        /// Returns best-matching category and similarity score.
        /// If score < min threshold, returns (null, score).
        /// </summary>
        public (string? category, float similarity) Infer(string text)
        {
            if (_knownVecs.Count == 0) return (null, 0f);

            var tokens = Tokenize(text);
            if (tokens.Count == 0) return (null, 0f);

            var q = ToTfidf(tokens);
            if (q.W.Count == 0) return (null, 0f);

            // Candidates via inverted index (token overlap)
            var candidateScores = new Dictionary<int, float>();
            foreach (var tok in q.W.Keys)
            {
                if (!_inv.TryGetValue(tok, out var ids)) continue;

                for (int i = 0; i < ids.Count; i++)
                {
                    int id = ids[i];
                    candidateScores.TryGetValue(id, out float s);
                    candidateScores[id] = s + 1f; // just to keep key present
                }
            }

            if (candidateScores.Count == 0) return (null, 0f);

            // Evaluate cosine only for candidates
            var best = new List<(int id, float sim)>(candidateScores.Count);

            foreach (var id in candidateScores.Keys)
            {
                float sim = Cosine(q, _knownVecs[id]);
                best.Add((id, sim));
            }

            // Take topK neighbors
            best.Sort((a, b) => b.sim.CompareTo(a.sim));
            if (best.Count > _topK) best.RemoveRange(_topK, best.Count - _topK);

            // Weighted vote by similarity
            var vote = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            float bestSim = 0f;

            foreach (var (id, sim) in best)
            {
                if (sim > bestSim) bestSim = sim;

                var cat = _knownCats[id];
                vote.TryGetValue(cat, out float v);
                vote[cat] = v + sim;
            }

            if (vote.Count == 0) return (null, bestSim);

            var winner = vote.OrderByDescending(kv => kv.Value).First().Key;

            if (bestSim < _minSimilarity)
                return (null, bestSim);

            return (winner, bestSim);
        }

        /// <summary>
        /// Helper: builds "folder + filename" text similar to your inferer.
        /// </summary>
        public static string BuildCombinedText(string fullPath)
        {
            var name = Path.GetFileNameWithoutExtension(fullPath) ?? "";
            var dir = Path.GetDirectoryName(fullPath) ?? "";

            var relParentsText = string.Join(" ", dir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .TakeLast(6));

            return (relParentsText + " " + name).ToLowerInvariant();
        }
    }
}
