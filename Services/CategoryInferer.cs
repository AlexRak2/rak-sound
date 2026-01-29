using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SonnissBrowser
{
    public sealed class CategoryInferer
    {
        // Split non-alphanumerics for fallback token soup if needed
        private static readonly Regex Splitter =
            new(@"[^a-z0-9]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // ----------------------------
        // 1) Prefix → (Tier1, Tier2)
        // Matches the Python PREFIX_MAP concept.
        // ----------------------------
        private static readonly Dictionary<string, (string Tier1, string Tier2)> PrefixMap =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // Ambient
                ["AMB"] = ("Ambient", "Ambience"),
                ["AMBUrbn"] = ("Ambient", "Ambience/Urban"),
                ["AMBTown"] = ("Ambient", "Ambience/Town"),
                ["AMBForst"] = ("Ambient", "Ambience/Forest"),
                ["AMBTran"] = ("Ambient", "Ambience/Transport"),
                ["AMBCnst"] = ("Ambient", "Ambience/Construction"),
                ["AMBInd"] = ("Ambient", "Ambience/Industrial"),
                ["AMBRoom"] = ("Ambient", "Ambience/Roomtone"),
                ["AMBPubl"] = ("Ambient", "Ambience/Public"),
                ["AMBNaut"] = ("Ambient", "Ambience/Nautical"),
                ["AMBRest"] = ("Ambient", "Ambience/Restaurant"),
                ["AMBUndr"] = ("Ambient", "Ambience/Underground"),
                ["AMBOffc"] = ("Ambient", "Ambience/Office"),
                ["AMBMisc"] = ("Ambient", "Ambience/Misc"),
                ["AMBDsgn"] = ("Ambient", "Ambience/Designed"),

                // Weather
                ["RAIN"] = ("Weather", "Rain"),
                ["RAINVege"] = ("Weather", "Rain"),
                ["WIND"] = ("Weather", "Wind"),
                ["WINDVege"] = ("Weather", "Wind"),

                // Water
                ["WATR"] = ("Water", "General"),
                ["WATRFlow"] = ("Water", "Flow"),
                ["WATRWave"] = ("Water", "Waves"),
                ["WATRSurf"] = ("Water", "Surf"),
                ["WATRLap"] = ("Water", "Lapping"),
                ["WATRDrip"] = ("Water", "Drips"),
                ["WATRDran"] = ("Water", "Drains"),
                ["WATRImpt"] = ("Water", "Impacts"),
                ["WATRMvmt"] = ("Water", "Movement"),
                ["WATRUndwtr"] = ("Water", "Underwater"),
                ["WATRFizz"] = ("Water", "Fizz"),
                ["AMBUndwtr"] = ("Water", "Underwater"),

                // UI
                ["UI"] = ("UI", "General"),
                ["UIClick"] = ("UI", "Clicks"),
                ["UIData"] = ("UI", "Data"),
                ["UIAlert"] = ("UI", "Alerts"),
                ["UIMvmt"] = ("UI", "Movement"),
                ["UIMisc"] = ("UI", "Misc"),

                // Weapons
                ["GUN"] = ("Weapons", "Guns"),
                ["GUNRif"] = ("Weapons", "Guns/Rifle"),
                ["GUNShotg"] = ("Weapons", "Guns/Shotgun"),
                ["GUNMech"] = ("Weapons", "Guns/Mechanics"),
                ["WEAP"] = ("Weapons", "General"),
                ["WEAPSwrd"] = ("Weapons", "Melee/Sword"),
                ["WEAPAxe"] = ("Weapons", "Melee/Axe"),
                ["WEAPArmr"] = ("Weapons", "Armor/Blocks"),
                ["SCIWeap"] = ("Weapons", "SciFi"),

                // Vehicles / transport
                ["VEH"] = ("Vehicles", "General"),
                ["VEHCar"] = ("Vehicles", "Cars"),
                ["VEHFarm"] = ("Vehicles", "Farm"),
                ["VEHAtv"] = ("Vehicles", "ATV"),
                ["VEHTire"] = ("Vehicles", "Tires"),
                ["VEHDoor"] = ("Vehicles", "Doors"),
                ["VEHHorn"] = ("Vehicles", "Horns"),
                ["TRN"] = ("Vehicles", "Trains"),
                ["TRNSbwy"] = ("Vehicles", "Subway"),
                ["TRNTram"] = ("Vehicles", "Tram"),
                ["BOAT"] = ("Vehicles", "Boats"),
                ["BOATMotr"] = ("Vehicles", "Boats/Motor"),
                ["BOATWash"] = ("Vehicles", "Boats/Wash"),
                ["BOATMech"] = ("Vehicles", "Boats/Mechanics"),
                ["BOATInt"] = ("Vehicles", "Boats/Interior"),

                // Foley / materials
                ["FOLY"] = ("Foley", "General"),
                ["OBJ"] = ("Foley", "Objects"),
                ["TOOL"] = ("Foley", "Tools"),
                ["CLOTH"] = ("Foley", "Cloth"),
                ["WOOD"] = ("Foley", "Wood"),
                ["METL"] = ("Foley", "Metal"),
                ["GLAS"] = ("Foley", "Glass"),
                ["PAPR"] = ("Foley", "Paper"),
                ["BELL"] = ("Foley", "Bells"),
                ["MACH"] = ("Foley", "Machines"),
                ["MECH"] = ("Foley", "Mechanisms"),

                // Doors (non-vehicle)
                ["DOOR"] = ("Doors", "General"),
                ["DOORCreak"] = ("Doors", "Creaks"),
                ["DOORGate"] = ("Doors", "Gates"),
                ["DOORHydr"] = ("Doors", "Hydraulic"),

                // Design / cinematic
                ["DSGN"] = ("Design", "General"),
                ["DSGNRise"] = ("Design", "Risers"),
                ["DSGNBoom"] = ("Design", "Booms/Hits"),
                ["DSGNDron"] = ("Design", "Drones"),
                ["DSGNErie"] = ("Design", "Eerie"),
                ["DSGNBram"] = ("Design", "Braams"),
                ["DSGNTonl"] = ("Design", "Tonal"),
                ["DSGNSrce"] = ("Design", "Source"),
                ["WHSH"] = ("Design", "Whooshes"),
                ["WHOOSH"] = ("Design", "Whooshes"),

                // Electrical / SciFi misc
                ["ELEC"] = ("Electrical", "General"),
                ["ELECEmf"] = ("Electrical", "EMF/Hum"),
                ["SCIMisc"] = ("SciFi", "Misc"),
                ["SCIMech"] = ("SciFi", "Mechanics"),

                // Animals / creatures
                ["ANML"] = ("Animals", "General"),
                ["BIRD"] = ("Animals", "Birds"),
                ["BIRDSong"] = ("Animals", "Birds/Song"),
                ["BIRDPrey"] = ("Animals", "Birds/Prey"),
                ["BIRDFowl"] = ("Animals", "Birds/Fowl"),
                ["CREA"] = ("Animals", "Creatures"),
                ["GRWL"] = ("Animals", "Growls"),
                ["ROAR"] = ("Animals", "Roars"),

                // Gore
                ["GORE"] = ("Gore", "General"),
                ["GOREBone"] = ("Gore", "Bone"),
                ["GOREFlsh"] = ("Gore", "Flesh"),
                ["GORESplt"] = ("Gore", "Splatter"),

                // Music
                ["MUSC"] = ("Music", "General"),
                ["MUSCStngr"] = ("Music", "Stingers"),
            };

        // ----------------------------
        // 2) Keyword fallback (Tier1/Tier2)
        // Mirrors the Python KEYWORDS.
        // ----------------------------
        private static readonly (string Tier1, string Tier2, string[] Keywords)[] KeywordRules =
        {
            ("Foley", "Footsteps", new[]
            {
                "footstep", "footsteps", "foot step", "foot steps"
            }),

            ("UI", "General", new[] { "ui", "interface", "button", "click", "menu", "select", "beep", "alert", "confirm", "cursor", "glitch" }),
            ("Weapons", "General", new[] { "gun", "shot", "reload", "weapon", "rifle", "pistol", "cannon", "grenade", "firing" }),
            ("Vehicles", "General", new[] { "car", "engine", "truck", "vehicle", "motor", "rpm", "pass-by", "pass by", "onboard", "onbrd", "drive", "horn", "exhaust", "gearshift" }),
            ("Ambient", "Ambience", new[] { "ambience", "ambient", "room tone", "roomtone", "atmos", "walla", "crowd", "station", "forest", "city", "subway" }),
            ("Water", "General", new[] { "water", "creek", "brook", "river", "wave", "splash", "drip", "underwater", "ocean" }),
            ("Doors", "General", new[] { "door", "gate", "hatch", "open", "close", "creak" }),
            ("Foley", "General", new[] { "foley", "handling", "grab", "drop", "cloth", "wood", "metal", "glass", "paper", "plastic" }),
            ("Design", "General", new[] { "riser", "whoosh", "hit", "boom", "cinematic", "trailer", "transition", "impact", "stinger" }),
            ("Voices", "General", new[] { "voice", "dialog", "dialogue", "announcement", "tannoy", "shout", "scream", "talk" }),
            ("Animals", "General", new[] { "bird", "dog", "cat", "pig", "goose", "rooster", "animal", "growl", "roar" }),
            ("Electrical", "General", new[] { "electric", "electrical", "emf", "hum", "interference", "arc", "discharge", "zap" }),
        };

        public (string tier1, string tier2, double confidence) InferTiered(string fullPath)
        {
            var name = Path.GetFileNameWithoutExtension(fullPath) ?? "";
            var dir = Path.GetDirectoryName(fullPath) ?? "";

            // combine folder + filename like the python
            var relParentsText = string.Join(" ", dir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                                    .Where(s => !string.IsNullOrWhiteSpace(s))
                                                    .TakeLast(6));
            var combinedText = (relParentsText + " " + name).ToLowerInvariant();

            // PASS 1: prefix mapping (token before first underscore)
            var prefixCat = DetectPrefixCategory(name);
            if (prefixCat != null)
                return (prefixCat.Value.Tier1, prefixCat.Value.Tier2, 0.92);

            // PASS 2: keyword fallback (folder + filename)
            foreach (var (t1, t2, kws) in KeywordRules)
            {
                for (int i = 0; i < kws.Length; i++)
                {
                    if (combinedText.Contains(kws[i]))
                        return (t1, t2, 0.72);
                }
            }

            // PASS 3: last folder fallback
            var lastFolder = dir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .LastOrDefault();

            if (!string.IsNullOrWhiteSpace(lastFolder))
                return ("Unsorted", lastFolder.Trim(), 0.15);

            return ("Unsorted", "Unsorted", 0.0);
        }

        /// <summary>
        /// Mirrors python's "token before underscore + progressive shortening".
        /// Example: "AMBUrbn_SomeName" -> AMBUrbn; if not found, tries AMBUrb, AMBUr, AMBU, AMB.
        /// </summary>
        private (string Tier1, string Tier2)? DetectPrefixCategory(string filenameNoExt)
        {
            // If there is no underscore, we still might have leading token,
            // but in these libraries it's typically prefix_...
            var token = filenameNoExt;
            var underscore = filenameNoExt.IndexOf('_');
            if (underscore > 0)
                token = filenameNoExt.Substring(0, underscore);

            // Exact match first
            if (PrefixMap.TryGetValue(token, out var hit))
                return hit;

            // Progressive shorten (down to length 3, because "AMB", "WATR", etc)
            for (int len = token.Length; len >= 3; len--)
            {
                var sub = token.Substring(0, len);
                if (PrefixMap.TryGetValue(sub, out hit))
                    return hit;
            }

            return null;
        }

        // If your UI expects a single string like the old Infer() did:
        public (string category, double confidence) Infer(string fullPath)
        {
            var (t1, t2, c) = InferTiered(fullPath);
            // choose one:
            // 1) Collapse: "Tier1/Tier2"
            // 2) Or just Tier1
            var cat = (t2 == "General" || string.IsNullOrWhiteSpace(t2)) ? t1 : $"{t1}/{t2}";
            return (cat, c);
        }
    }
}
