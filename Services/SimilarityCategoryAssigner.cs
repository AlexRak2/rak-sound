using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SonnissBrowser
{
    /// <summary>
    /// Second-pass categorizer:
    /// - TF-IDF for word tokens + character n-grams (handles naming variations)
    /// - Inverted index for candidate retrieval (word + ngram)
    /// - kNN vote + category centroid fallback
    /// </summary>
    public sealed class SimilarityCategoryAssigner
    {
        private static readonly Regex Splitter =
            new(@"[^a-z0-9]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Stop words + common junk in libraries
        private static readonly HashSet<string> Stop =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "a","an","and","or","the","to","of","in","on","for","with","at","by","from",
                "mono","stereo","ms","xy","ortf","decoded","wide","near","far","field","room","mic","mics",
                "loop","loops","oneshot","one","shot","take","takes","final","edit","mix","master",
                "ext","int","interior","exterior",
                "v1","v2","v3","v4","v5",
                "sfx","fx","wav","mp3","flac","ogg","aiff","aif","aac","m4a"
            };

        // Simple synonym/normalization map (big wins for almost no work)
        private static readonly Dictionary<string, string> Syn =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["bys"] = "passby",
                ["flyby"] = "passby",
                ["pass"] = "passby",
                ["pass-bys"] = "passby",
                ["passbys"] = "passby",
                ["rev"] = "engine",
                ["revs"] = "engine",
                ["rpm"] = "engine",
                ["drone"] = "drone",
                ["uplifter"] = "rise",
                ["riser"] = "rise",
                ["whooshes"] = "whoosh",
                ["whoosh"] = "whoosh",
                ["amb"] = "ambience",
                ["atmo"] = "ambience",
                ["atmos"] = "ambience",
                ["crowds"] = "crowd",
                ["applauding"] = "applause"
            };

        private static bool IsBadToken(string t)
        {
            if (t.Length <= 2) return true;
            int digits = 0;
            for (int i = 0; i < t.Length; i++)
                if (char.IsDigit(t[i])) digits++;
            return digits >= t.Length - 1;
        }

        private static string NormalizeToken(string t)
        {
            if (Syn.TryGetValue(t, out var s)) return s;
            return t;
        }

        private static List<string> TokenizeWords(string text)
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

                t = NormalizeToken(t);
                if (Stop.Contains(t)) continue;
                if (IsBadToken(t)) continue;

                tokens.Add(t);
            }
            return tokens;
        }

        // Character n-grams catch variation like pass-by/passby/pass_by and vendor naming weirdness
        private static List<string> TokenizeCharNgrams(string text, int minN = 3, int maxN = 5)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<string>();

            // keep only letters/digits, collapse spaces
            var clean = Regex.Replace(text.ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();
            clean = Regex.Replace(clean, @"\s+", " ");

            // join words with underscore to preserve boundaries a bit
            clean = clean.Replace(' ', '_');

            if (clean.Length < minN) return new List<string>();

            var grams = new List<string>(clean.Length * 2);

            for (int n = minN; n <= maxN; n++)
            {
                if (clean.Length < n) continue;
                for (int i = 0; i <= clean.Length - n; i++)
                {
                    // prefix to separate from word tokens
                    grams.Add("#" + clean.Substring(i, n));
                }
            }

            return grams;
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

        // inverted index over *features* (words + char grams)
        private readonly Dictionary<string, List<int>> _inv = new(StringComparer.OrdinalIgnoreCase);

        // category centroids (prototype vectors)
        private readonly Dictionary<string, Vec> _catCentroid = new(StringComparer.OrdinalIgnoreCase);

        private readonly int _topK;
        private readonly float _minSimilarity;
        private readonly float _minCentroidSimilarity;

        public SimilarityCategoryAssigner(int topK = 9, float minSimilarity = 0.22f, float minCentroidSimilarity = 0.18f)
        {
            _topK = Math.Max(1, topK);
            _minSimilarity = minSimilarity;
            _minCentroidSimilarity = minCentroidSimilarity;
        }

        /// <summary>
        /// Build index from known-good categorized items.
        /// text should include folder + filename (your BuildCombinedText is fine)
        /// </summary>
        public void BuildIndex(IEnumerable<(string text, string category)> known)
        {
            _idf.Clear();
            _knownVecs.Clear();
            _knownCats.Clear();
            _inv.Clear();
            _catCentroid.Clear();

            // 1) Collect tokenized docs and DF
            var docs = new List<List<string>>();
            var cats = new List<string>();
            var df = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var (text, category) in known)
            {
                // combine word tokens + char ngrams
                var toks = TokenizeWords(text);
                toks.AddRange(TokenizeCharNgrams(text));

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

            // 2) Compute IDF (smooth)
            foreach (var kv in df)
            {
                var idf = (float)(Math.Log((N + 1.0) / (kv.Value + 1.0)) + 1.0);
                _idf[kv.Key] = idf;
            }

            // centroid accumulators
            var centroidSum = new Dictionary<string, Dictionary<string, float>>(StringComparer.OrdinalIgnoreCase);
            var centroidCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // 3) Build known vectors + inverted index
            for (int i = 0; i < docs.Count; i++)
            {
                var vec = ToTfidf(docs[i]);
                if (vec.W.Count == 0) continue;

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

                // add to centroid sums
                var cat = cats[i];
                if (!centroidSum.TryGetValue(cat, out var sum))
                {
                    sum = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
                    centroidSum[cat] = sum;
                    centroidCount[cat] = 0;
                }

                foreach (var kv in vec.W)
                {
                    sum.TryGetValue(kv.Key, out var cur);
                    sum[kv.Key] = cur + kv.Value;
                }
                centroidCount[cat] = centroidCount[cat] + 1;
            }

            // 4) finalize centroids (average + compute norm)
            foreach (var cat in centroidSum.Keys)
            {
                var sum = centroidSum[cat];
                var count = Math.Max(1, centroidCount[cat]);

                var v = new Vec();
                float sumSq = 0f;

                foreach (var kv in sum)
                {
                    float w = kv.Value / count;
                    v.W[kv.Key] = w;
                    sumSq += w * w;
                }

                v.Norm = (float)Math.Sqrt(sumSq);
                if (v.Norm < 1e-6f) v.Norm = 1e-6f;

                _catCentroid[cat] = v;
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

                // Sublinear TF
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
        /// kNN first; if weak, centroid fallback.
        /// </summary>
        public (string? category, float similarity) Infer(string text)
        {
            if (_knownVecs.Count == 0) return (null, 0f);

            // query features
            var toks = TokenizeWords(text);
            toks.AddRange(TokenizeCharNgrams(text));

            if (toks.Count == 0) return (null, 0f);

            var q = ToTfidf(toks);
            if (q.W.Count == 0) return (null, 0f);

            // Candidates via inverted index (feature overlap)
            var candidates = new Dictionary<int, int>(); // id -> hit count
            foreach (var tok in q.W.Keys)
            {
                if (!_inv.TryGetValue(tok, out var ids)) continue;
                for (int i = 0; i < ids.Count; i++)
                {
                    int id = ids[i];
                    candidates.TryGetValue(id, out int c);
                    candidates[id] = c + 1;
                }
            }

            // If nothing overlaps (rare now because char ngrams help), go centroid-only
            if (candidates.Count == 0)
                return CentroidOnly(q);

            // Evaluate cosine for candidates
            var best = new List<(int id, float sim)>(candidates.Count);
            foreach (var id in candidates.Keys)
            {
                float sim = Cosine(q, _knownVecs[id]);
                best.Add((id, sim));
            }

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

            // Winner from kNN
            string? winner = vote.Count > 0 ? vote.OrderByDescending(kv => kv.Value).First().Key : null;

            // If strong enough, accept
            if (winner != null && bestSim >= _minSimilarity)
                return (winner, bestSim);

            // Otherwise try centroid fallback (often saves a ton)
            var (centCat, centSim) = BestCentroid(q);
            if (centCat != null && centSim >= _minCentroidSimilarity)
                return (centCat, centSim);

            // Nothing reliable
            return (null, Math.Max(bestSim, centSim));
        }

        private (string? category, float similarity) CentroidOnly(Vec q)
        {
            var (cat, sim) = BestCentroid(q);
            if (cat != null && sim >= _minCentroidSimilarity) return (cat, sim);
            return (null, sim);
        }

        private (string? category, float similarity) BestCentroid(Vec q)
        {
            string? bestCat = null;
            float bestSim = 0f;

            foreach (var kv in _catCentroid)
            {
                float sim = Cosine(q, kv.Value);
                if (sim > bestSim)
                {
                    bestSim = sim;
                    bestCat = kv.Key;
                }
            }
            return (bestCat, bestSim);
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
