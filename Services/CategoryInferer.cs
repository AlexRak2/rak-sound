using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SonnissBrowser
{
    public sealed class CategoryInferer
    {
        // split non-alphanumerics
        private static readonly Regex Splitter =
            new(@"[^a-z0-9]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // keep your existing PrefixMap here (unchanged)
        private static readonly Dictionary<string, (string Tier1, string Tier2)> PrefixMap =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // (PASTE YOUR EXISTING PrefixMap CONTENT HERE)
            };

        // ----------------------------
        // Strong “vendor/prefix” patterns (these hit HARD)
        // ----------------------------
        private static readonly (Regex Rx, string Tier1, string Tier2, int Score)[] VendorRules =
        {
            (new Regex(@"^bluezone_", RegexOptions.IgnoreCase | RegexOptions.Compiled), "SciFi", "Designed", 80),
            (new Regex(@"^bn_", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Design", "Tension", 60),
            (new Regex(@"^efx\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Design", "FX", 55),
            (new Regex(@"^pm_", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Design", "FX", 50),
            (new Regex(@"^detunized_", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Design", "Atmospheres", 55),
            (new Regex(@"^csfx-", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Design", "FX", 45),
            (new Regex(@"^g4f\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "UI", "Game", 45),
        };

        // ----------------------------
        // Weighted phrase rules (highest signal)
        // If these phrases appear, we can be very confident.
        // ----------------------------
        private static readonly (string Tier1, string Tier2, string[] Phrases, int ScorePerHit)[] PhraseRules =
        {
            ("Weather", "Rain", new[] { "rain hard", "rain urban", "rain mountain", "rain loop", "rain drainage" }, 40),
            ("Weather", "Wind", new[] { "wind turbine", "wind hurricane", "wind strong", "gusty", "blustery" }, 40),
            ("Vehicles", "Passbys", new[] { "passby", "pass by", "fly by", "bys", "doppler" }, 45),
            ("Vehicles", "Aircraft", new[] { "helicopter", "jet fighter", "f-16", "mig", "spitfire", "cessna", "hawk jets", "b-25", "p-51" }, 35),
            ("Crowds", "Applause", new[] { "audience applauding", "applause", "cheers", "whistlings", "crowd" }, 45),
            ("Weapons", "Guns", new[] { "ak-74", "m16", "mg4", "saiga", "beretta", "desert eagle", "rounds", "burst" }, 45),
            ("Alarms", "Sirens", new[] { "police siren", "ambulance", "emergency siren", "siren" }, 45),
            ("Explosions", "Fireworks", new[] { "fireworks", "stanley park", "kmr", "slapbacks" }, 45),
            ("Explosions", "General", new[] { "big explosion", "medium explosion", "small explosion", "air explosion", "tnt explosion", "c4 explosion" }, 45),
            ("Fire", "Campfire", new[] { "campfire", "fireplace", "burning house" }, 40),
            ("Foley", "Page Turns", new[] { "page turn", "turn page", "magazine", "book" }, 35),
            ("Foley", "Coins/Money", new[] { "counting money", "coin bag", "coins", "bills" }, 35),
            ("Foley", "Kitchen", new[] { "knife sharpening", "cutting board", "kitchen", "frying", "sizzle", "kettle", "toaster" }, 30),
            ("Foley", "Bathroom", new[] { "toilet", "flushing", "washing hands", "sink", "draining" }, 35),
            ("SciFi", "Robots", new[] { "robot", "droid", "servo", "scan data", "cyberdeck", "nanotech" }, 35),
            ("Music", "Tonal", new[] { "pad", "drone", "tonal", "braams", "swell", "uplifter" }, 25),
            ("Gore", "Body", new[] { "beheading", "bone", "flesh", "blood", "gore", "skinning" }, 45),
            ("Animals", "Insects", new[] { "crickets", "cicada", "bees", "ants", "insect" }, 35),
            ("Animals", "Birds", new[] { "robins", "ravens", "bird" }, 35),
            ("Sports", "Bowling", new[] { "bowling", "pins", "ball return", "strike", "gutter" }, 55),
        };

        // ----------------------------
        // Token rules (medium signal, stacks nicely)
        // token hits add up across multiple words
        // ----------------------------
        private static readonly (string Tier1, string Tier2, string[] Tokens, int ScorePerHit)[] TokenRules =
        {
            ("Weather", "Rain", new[] { "rain", "drizzle", "storm", "thunderstorm", "hail" }, 8),
            ("Weather", "Wind", new[] { "wind", "gust", "hurricane", "whirlwind", "howling" }, 8),
            ("Water", "General", new[] { "water", "river", "creek", "wave", "ocean", "brook", "underwater" }, 7),

            ("Explosions", "General", new[] { "explosion", "blast", "detonation", "boomer", "impact" }, 9),
            ("Fire", "General", new[] { "fire", "burning", "flame", "torch", "campfire", "fireplace" }, 8),

            ("Vehicles", "General", new[] { "car", "truck", "engine", "rpm", "exhaust", "gear", "diesel" }, 7),
            ("Vehicles", "Aircraft", new[] { "helicopter", "jet", "plane", "aircraft", "takeoff", "landing" }, 7),
            ("Vehicles", "Train", new[] { "train", "tram", "subway", "rail" }, 7),
            ("Vehicles", "Boat", new[] { "boat", "ship", "gondola", "harbor" }, 6),

            ("Alarms", "General", new[] { "alarm", "warning", "alert" }, 8),
            ("Alarms", "Sirens", new[] { "siren", "police", "ambulance", "emergency" }, 9),

            ("Foley", "General", new[] { "cloth", "zipper", "velcro", "drawer", "latch", "tape", "bottle", "case" }, 6),
            ("Foley", "Kitchen", new[] { "knife", "cut", "chop", "kettle", "toast", "coffee", "grinder" }, 6),
            ("Foley", "Office", new[] { "pen", "pencil", "paper", "typewriter", "printer", "scanner" }, 6),

            ("SciFi", "General", new[] { "scifi", "sci", "laser", "beam", "telemetry", "hologram" }, 8),
            ("SciFi", "Robots", new[] { "robot", "servo", "droid", "android", "mech", "scan" }, 8),

            ("Crowds", "General", new[] { "crowd", "walla", "protest", "market", "busy" }, 7),

            ("Animals", "General", new[] { "animal", "dog", "cat", "pony", "horse", "coyote" }, 6),
            ("Animals", "Insects", new[] { "cricket", "cicada", "bees", "ants", "fly" }, 7),

            ("Gore", "General", new[] { "gore", "blood", "bone", "flesh", "stab", "beheading" }, 10),

            ("Music", "Tonal", new[] { "drone", "pad", "swell", "tonal", "braam", "glissando" }, 5),
            ("UI", "General", new[] { "ui", "menu", "select", "click", "beep", "confirm", "gameover", "powerup" }, 7),
        };

        // misc “stopwords” you don’t want scoring (these appear in tons of filenames)
        private static readonly HashSet<string> StopTokens = new(StringComparer.OrdinalIgnoreCase)
        {
            "mono","stereo","ms","xy","ortf","decoded","wide","near","far","field","loop","loops",
            "take","takes","version","v1","v2","perspective","front","rear","left","right","mid","distant",
            "t1","t2","t3","t4","t5","t6","t7","t8","t9","t10","t11","t12","t13","t14","t15","t16","t17","t18","t19","t20",
            "ext","int","interior","exterior"
        };

        public (string tier1, string tier2, double confidence) InferTiered(string fullPath)
        {
            var name = Path.GetFileNameWithoutExtension(fullPath) ?? "";
            var dir = Path.GetDirectoryName(fullPath) ?? "";

            // last N folders help a lot
            var relParentsText = string.Join(" ", dir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                                    .Where(s => !string.IsNullOrWhiteSpace(s))
                                                    .TakeLast(6));

            // normalized string (spaces only)
            var combined = Normalize(relParentsText + " " + name);

            // tokens
            var tokens = Splitter.Split(combined)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Where(t => t.Length >= 2)
                .Where(t => !StopTokens.Contains(t))
                .ToArray();

            // 1) your existing prefix logic (still great when present)
            var prefixCat = DetectPrefixCategory(name);
            if (prefixCat != null)
                return (prefixCat.Value.Tier1, prefixCat.Value.Tier2, 0.93);

            // 2) weighted scoring
            var scores = new Dictionary<(string t1, string t2), int>();

            void AddScore(string t1, string t2, int s)
            {
                var key = (t1, t2);
                scores.TryGetValue(key, out var cur);
                scores[key] = cur + s;
            }

            // vendor rules (based on filename start)
            foreach (var (rx, t1, t2, sc) in VendorRules)
            {
                if (rx.IsMatch(name))
                    AddScore(t1, t2, sc);
            }

            // phrase rules (search in normalized combined text)
            foreach (var (t1, t2, phrases, perHit) in PhraseRules)
            {
                for (int i = 0; i < phrases.Length; i++)
                {
                    if (combined.Contains(Normalize(phrases[i]), StringComparison.OrdinalIgnoreCase))
                        AddScore(t1, t2, perHit);
                }
            }

            // token rules (count token hits)
            var tokenSet = new HashSet<string>(tokens, StringComparer.OrdinalIgnoreCase);
            foreach (var (t1, t2, ruleTokens, perHit) in TokenRules)
            {
                int hits = 0;
                for (int i = 0; i < ruleTokens.Length; i++)
                {
                    var tk = Normalize(ruleTokens[i]);
                    if (tokenSet.Contains(tk)) hits++;
                }

                if (hits > 0)
                    AddScore(t1, t2, hits * perHit);
            }

            // 3) pick best
            if (scores.Count > 0)
            {
                var ordered = scores.OrderByDescending(kv => kv.Value).ToList();
                var best = ordered[0];
                var bestScore = best.Value;
                var secondScore = ordered.Count > 1 ? ordered[1].Value : 0;

                // confidence based on “how much the winner wins by”
                var gap = bestScore - secondScore;

                // clamp-ish mapping
                double confidence =
                    bestScore >= 80 ? 0.95 :
                    bestScore >= 55 ? 0.88 :
                    bestScore >= 35 ? 0.78 :
                    bestScore >= 22 ? 0.65 :
                    0.45;

                // if it barely beat second place, reduce confidence
                if (gap <= 6) confidence -= 0.12;
                if (gap <= 2) confidence -= 0.18;

                confidence = Math.Clamp(confidence, 0.10, 0.98);

                return (best.Key.t1, best.Key.t2, confidence);
            }

            // 4) last folder fallback
            var lastFolder = dir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .LastOrDefault();

            if (!string.IsNullOrWhiteSpace(lastFolder))
                return ("Unsorted", lastFolder.Trim(), 0.15);

            return ("Unsorted", "Unsorted", 0.0);
        }

        private static string Normalize(string s)
        {
            s = (s ?? "").ToLowerInvariant();
            s = s.Replace("’", "'").Replace("–", "-").Replace("—", "-");
            // convert separators into spaces
            s = s.Replace("_", " ").Replace("-", " ").Replace(",", " ").Replace(".", " ").Replace("(", " ").Replace(")", " ");
            s = Regex.Replace(s, @"\s+", " ").Trim();
            return s;
        }

        /// <summary>
        /// token before underscore + progressive shortening
        /// </summary>
        private (string Tier1, string Tier2)? DetectPrefixCategory(string filenameNoExt)
        {
            var token = filenameNoExt;
            var underscore = filenameNoExt.IndexOf('_');
            if (underscore > 0)
                token = filenameNoExt.Substring(0, underscore);

            if (PrefixMap.TryGetValue(token, out var hit))
                return hit;

            for (int len = token.Length; len >= 3; len--)
            {
                var sub = token.Substring(0, len);
                if (PrefixMap.TryGetValue(sub, out hit))
                    return hit;
            }

            return null;
        }

        // if you still want a single collapsed category string
        public (string category, double confidence) Infer(string fullPath)
        {
            var (t1, t2, c) = InferTiered(fullPath);
            var cat = (t2 == "General" || string.IsNullOrWhiteSpace(t2)) ? t1 : $"{t1}/{t2}";
            return (cat, c);
        }
    }
}
