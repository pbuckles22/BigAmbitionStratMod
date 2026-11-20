using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class StoreOpsFacts
    {
        public StoreOpsFacts(
            int dirtSpots,
            decimal sales,
            decimal theft,
            bool uniformsDemanded,
            bool canMeetUniforms)
        {
            DirtSpots = dirtSpots;
            Sales = sales;
            Theft = theft;
            UniformsDemanded = uniformsDemanded;
            CanMeetUniforms = canMeetUniforms;
        }

        public int DirtSpots { get; }

        public decimal Sales { get; }

        public decimal Theft { get; }

        public bool UniformsDemanded { get; }

        public bool CanMeetUniforms { get; }
    }

    public sealed class StoreOpsPick
    {
        public StoreOpsPick(
            bool assigned,
            string cleaner,
            string guard,
            bool uniforms,
            string cleanerCall,
            string guardCall,
            string uniformsCall)
        {
            Assigned = assigned;
            Cleaner = cleaner ?? "";
            Guard = guard ?? "";
            Uniforms = uniforms;
            CleanerCall = cleanerCall ?? "";
            GuardCall = guardCall ?? "";
            UniformsCall = uniformsCall ?? "";
        }

        public bool Assigned { get; }

        public string Cleaner { get; }

        public string Guard { get; }

        public bool Uniforms { get; }

        public string CleanerCall { get; }

        public string GuardCall { get; }

        public string UniformsCall { get; }
    }

    public sealed class StoreFixtures
    {
        public StoreFixtures(bool cleaningStation, bool guardLocker, bool uniformLocker)
        {
            CleaningStation = cleaningStation;
            GuardLocker = guardLocker;
            UniformLocker = uniformLocker;
        }

        public bool CleaningStation { get; }

        public bool GuardLocker { get; }

        public bool UniformLocker { get; }

        public static StoreFixtures Present()
        {
            return new StoreFixtures(true, true, true);
        }

        public static StoreFixtures GiftShopWholesalers()
        {
            return new StoreFixtures(true, false, true);
        }

        public static StoreFixtures FromNames(System.Collections.Generic.IEnumerable<string>? names)
        {
            var cleaning = false;
            var guard = false;
            var uniform = false;
            if (names != null)
            {
                foreach (var name in names)
                {
                    if (name == "ba:itemname_cleaningstation")
                    {
                        cleaning = true;
                    }

                    if (name == "ba:itemname_securityguardlocker")
                    {
                        guard = true;
                    }

                    if (name == "ba:itemname_uniformlocker")
                    {
                        uniform = true;
                    }
                }
            }

            return new StoreFixtures(cleaning, guard, uniform);
        }
    }

    public static class StoreOps
    {
        public const decimal TheftMax = 0.01m;

        public const string SpawnPrefab = "Characters/HumanDefinitionLow";

        public static string UnstartedType(string? role)
        {
            if (role == "Cleaner")
            {
                return "CleanerEmployee";
            }

            if (role == "Guard")
            {
                return "SecurityGuardEmployee";
            }

            return "";
        }

        public static string BodySource(
            StoreOpsPick? pick,
            bool stationHasPerson,
            bool recordHasPerson,
            bool stationAssigned = false)
        {
            if (pick == null || !pick.Assigned || (pick.Cleaner.Length == 0 && pick.Guard.Length == 0))
            {
                return "";
            }

            if (stationAssigned)
            {
                return "kept";
            }

            if (stationHasPerson)
            {
                return "station";
            }

            if (recordHasPerson)
            {
                return "record";
            }

            return "spawn";
        }

        public static string Outcome(StoreOpsPick? pick, string? source)
        {
            if (pick == null || !pick.Assigned)
            {
                return "missed";
            }

            if (pick.Cleaner.Length == 0 && pick.Guard.Length == 0)
            {
                return pick.Uniforms ? "assigned" : "missed";
            }

            if (source == "kept" || source == "station" || source == "record" || source == "spawn")
            {
                return "assigned";
            }

            return "missed";
        }

        public static StoreOpsFacts Opening()
        {
            return new StoreOpsFacts(1, 100m, 2m, false, false);
        }

        public static StoreOpsPick Assign(StarterPick? layout, StoreOpsFacts? facts, ApiNote? api)
        {
            return Assign(layout, facts, StoreFixtures.Present(), api);
        }

        public static StoreOpsPick Assign(
            StarterPick? layout,
            StoreOpsFacts? facts,
            StoreFixtures? fixtures,
            ApiNote? api)
        {
            if (layout == null || !layout.Loaded || facts == null || fixtures == null || api == null)
            {
                return None();
            }

            var cleanerCall = api.Call("cleaner");
            var guardCall = api.Call("guard");
            var uniformsCall = api.Call("uniforms");
            var cleaner = facts.DirtSpots > 0 && fixtures.CleaningStation && cleanerCall.Length > 0 ? "Cleaner" : "";
            var guard = "";
            if (facts.Sales > 0m
                && facts.Theft > facts.Sales * TheftMax
                && fixtures.GuardLocker
                && guardCall.Length > 0)
            {
                guard = "Guard";
            }

            var uniforms = facts.UniformsDemanded
                && facts.CanMeetUniforms
                && fixtures.UniformLocker
                && uniformsCall.Length > 0;
            if (cleaner.Length == 0 && guard.Length == 0 && !uniforms)
            {
                return None();
            }

            return new StoreOpsPick(
                true,
                cleaner,
                guard,
                uniforms,
                cleaner.Length == 0 ? "" : cleanerCall,
                guard.Length == 0 ? "" : guardCall,
                uniforms ? uniformsCall : "");
        }

        public static string Record(string localAppData, StoreOpsPick? pick, BuildingRent? rent, int day)
        {
            if (pick == null || !pick.Assigned || rent == null || !rent.Rented)
            {
                return "";
            }

            var line = "ops.assigned version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + rent.Street
                + " number=" + rent.Number.ToString(CultureInfo.InvariantCulture)
                + " cleaner=" + pick.Cleaner
                + " guard=" + pick.Guard
                + " uniforms=" + (pick.Uniforms ? "true" : "false")
                + " cleanerCall=" + pick.CleanerCall
                + " guardCall=" + pick.GuardCall
                + " uniformsCall=" + pick.UniformsCall;
            return AppendOnce(localAppData, line);
        }

        public static string Miss(string localAppData, StoreOpsPick pick, BuildingRent rent, int day, string reason)
        {
            var line = "ops.missed version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + rent.Street
                + " number=" + rent.Number.ToString(CultureInfo.InvariantCulture)
                + " cleaner=" + pick.Cleaner
                + " guard=" + pick.Guard
                + " uniforms=" + (pick.Uniforms ? "true" : "false")
                + " reason=" + (reason ?? "");
            return Append(localAppData, line);
        }

        public const int VerifyFrames = 300;

        public static bool Retry(string? reason)
        {
            return reason == "notstarted"
                || reason == "ops-station-not-initialized-frame0"
                || reason == "nostation"
                || reason == "nolocker"
                || reason == "nouniform"
                || reason == "nocharacter";
        }

        public static string UniformReason(bool locker, bool saveReady, string? presetId, bool dressed)
        {
            if (!locker)
            {
                return "nolocker";
            }

            if (!saveReady)
            {
                return "nosave";
            }

            if (string.IsNullOrEmpty(presetId))
            {
                return "nopreset";
            }

            return dressed ? "" : "noskill";
        }

        public static string CleanerLine(string localAppData, BuildingRent? rent, int day, StoreOpsCleaner? cleaner)
        {
            if (rent == null || !rent.Rented || cleaner == null)
            {
                return "";
            }

            var placed = cleaner.Shift && cleaner.Person && cleaner.Type == UnstartedType("Cleaner");
            var line = "ops.cleaner version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + rent.Street
                + " number=" + rent.Number.ToString(CultureInfo.InvariantCulture)
                + " employee=" + cleaner.EmployeeId
                + " name=" + cleaner.Name.Replace(' ', '_')
                + " station=" + cleaner.StationId
                + " shift=" + (cleaner.Shift ? "true" : "false")
                + " person=" + (cleaner.Person ? "true" : "false")
                + " type=" + cleaner.Type
                + " placed=" + (placed ? "true" : "false");
            return AppendOnce(localAppData, line);
        }

        public const string CleanerSkill = "ba:skill_cleaning";

        public static string UniformPreset(IEnumerable<string>? savedIds, string? businessDefaultId)
        {
            if (savedIds != null)
            {
                foreach (var id in savedIds)
                {
                    if (!string.IsNullOrEmpty(id))
                    {
                        return id;
                    }
                }
            }

            return businessDefaultId ?? "";
        }

        public static List<string> UniformSkills(
            IEnumerable<string>? primarySkills,
            IEnumerable<string>? requiredSkills,
            string? cleanerSkill)
        {
            var skills = new List<string>();
            void Add(string? skill)
            {
                if (!string.IsNullOrEmpty(skill) && !skills.Contains(skill!))
                {
                    skills.Add(skill!);
                }
            }

            Add(cleanerSkill);
            foreach (var skill in primarySkills ?? new string[0])
            {
                Add(skill);
            }

            foreach (var skill in requiredSkills ?? new string[0])
            {
                Add(skill);
            }

            return skills;
        }

        public static List<string> SeedUniforms(IDictionary<string, string> bySkill, IEnumerable<string> skills, string? presetId)
        {
            var seeded = new List<string>();
            if (bySkill == null || skills == null || string.IsNullOrEmpty(presetId))
            {
                return seeded;
            }

            foreach (var skill in skills)
            {
                if (string.IsNullOrEmpty(skill) || bySkill.ContainsKey(skill))
                {
                    continue;
                }

                bySkill[skill] = presetId!;
                seeded.Add(skill);
            }

            return seeded;
        }

        public static bool Dressed(IDictionary<string, string>? bySkill, string skill)
        {
            return bySkill != null
                && bySkill.TryGetValue(skill, out var presetId)
                && !string.IsNullOrEmpty(presetId);
        }

        public static bool Kept(StoreOpsPick? pick, bool shift, bool person, string? type, string? uniform)
        {
            if (pick == null || !pick.Assigned)
            {
                return false;
            }

            if (pick.Uniforms && string.IsNullOrEmpty(uniform))
            {
                return false;
            }

            if (pick.Cleaner.Length == 0)
            {
                return true;
            }

            return shift && person && type == UnstartedType(pick.Cleaner);
        }

        public static string Verify(
            string localAppData,
            StoreOpsPick? pick,
            BuildingRent? rent,
            int day,
            bool shift,
            bool person,
            string? type,
            string? uniform)
        {
            if (pick == null || !pick.Assigned || rent == null || !rent.Rented)
            {
                return "";
            }

            var line = "ops.verified version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + rent.Street
                + " number=" + rent.Number.ToString(CultureInfo.InvariantCulture)
                + " cleaner=" + pick.Cleaner
                + " shift=" + (shift ? "true" : "false")
                + " person=" + (person ? "true" : "false")
                + " type=" + (type ?? "")
                + " uniform=" + (string.IsNullOrEmpty(uniform) ? "nopreset" : uniform)
                + " kept=" + (Kept(pick, shift, person, type, uniform) ? "true" : "false");
            return Append(localAppData, line);
        }

        public static string Skip(string localAppData, int day)
        {
            var line = "ops.skipped version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " reason=open";
            return Append(localAppData, line);
        }

        private static string AppendOnce(string localAppData, string line)
        {
            var path = LoadLog.CeoLogPath(localAppData ?? "");
            if (File.Exists(path))
            {
                foreach (var existing in File.ReadAllLines(path))
                {
                    if (existing == line)
                    {
                        CeoLedger.Ingest(line);
                        return line;
                    }
                }
            }

            return Append(localAppData ?? "", line);
        }

        private static string Append(string localAppData, string line)
        {
            var path = LoadLog.CeoLogPath(localAppData ?? "");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + System.Environment.NewLine);
            CeoLedger.Ingest(line);
            return line;
        }

        private static StoreOpsPick None()
        {
            return new StoreOpsPick(false, "", "", false, "", "", "");
        }
    }

    public sealed class StoreOpsCleaner
    {
        public StoreOpsCleaner(string? employeeId, string? name, string? stationId, bool shift, bool person, string? type)
        {
            EmployeeId = employeeId ?? "";
            Name = name ?? "";
            StationId = stationId ?? "";
            Shift = shift;
            Person = person;
            Type = type ?? "";
        }

        public string EmployeeId { get; }

        public string Name { get; }

        public string StationId { get; }

        public bool Shift { get; }

        public bool Person { get; }

        public string Type { get; }
    }

    public sealed class StoreOpsWatch
    {
        private object? _seen;
        private int _tries;

        public bool Due(object? save)
        {
            return save != null && !ReferenceEquals(_seen, save);
        }

        public bool Waiting()
        {
            return _tries < 45;
        }

        public void Note()
        {
            _tries++;
        }

        public void Mark(object? save)
        {
            _seen = save;
        }

        private int _verifyLeft;

        public string VerifyStreet { get; private set; } = "";

        public int VerifyNumber { get; private set; }

        public int VerifyDay { get; private set; }

        public StoreOpsPick? VerifyPick { get; private set; }

        public void Arm(string street, int number, int day, int frames, StoreOpsPick? pick = null)
        {
            VerifyStreet = street ?? "";
            VerifyNumber = number;
            VerifyDay = day;
            VerifyPick = pick;
            _verifyLeft = frames < 1 ? 1 : frames;
        }

        public bool VerifyDue()
        {
            if (_verifyLeft <= 0)
            {
                return false;
            }

            _verifyLeft--;
            return _verifyLeft == 0;
        }
    }
}
