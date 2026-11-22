using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class StaffSense
    {
        public StaffSense(bool registerStaffed, bool cleanerStaffed, float satisfaction)
        {
            RegisterStaffed = registerStaffed;
            CleanerStaffed = cleanerStaffed;
            Satisfaction = satisfaction;
        }

        public bool RegisterStaffed { get; }

        public bool CleanerStaffed { get; }

        public float Satisfaction { get; }

        public bool Staffed =>
            RegisterStaffed
            && CleanerStaffed
            && Satisfaction >= StoreHealthGate.SatisfactionFloor;
    }

    public sealed class StaffShift
    {
        public const string Register = "register";

        public const string Cleaner = "cleaner";

        public StaffShift(string? employeeId, string? itemInstanceId, string? kind)
        {
            EmployeeId = employeeId ?? "";
            ItemInstanceId = itemInstanceId ?? "";
            Kind = kind ?? "";
        }

        public string EmployeeId { get; }

        public string ItemInstanceId { get; }

        public string Kind { get; }
    }

    public static class StoreStaff
    {
        public static StaffSense Sense(
            bool registerAssigned,
            bool cleanerAssigned,
            float satisfaction,
            int employeeHeadcount = 0)
        {
            _ = employeeHeadcount;
            return new StaffSense(registerAssigned, cleanerAssigned, satisfaction);
        }

        public static StaffSense Sense(StaffSense? stations)
        {
            if (stations == null)
            {
                return new StaffSense(false, false, 0f);
            }

            return Sense(stations.RegisterStaffed, stations.CleanerStaffed, stations.Satisfaction);
        }

        public static StaffSense FromShifts(
            IReadOnlyList<StaffShift>? shifts,
            bool warnedNoEmployee,
            float satisfactionWhenOk = 100f)
        {
            if (shifts == null || shifts.Count == 0)
            {
                return Sense(false, false, 0f);
            }

            var register = false;
            var cleaner = false;
            foreach (var shift in shifts)
            {
                if (shift == null || shift.EmployeeId.Length == 0)
                {
                    continue;
                }

                if (shift.Kind == StaffShift.Register)
                {
                    register = true;
                }
                else if (shift.Kind == StaffShift.Cleaner)
                {
                    cleaner = true;
                }
            }

            var satisfaction = register && cleaner && !warnedNoEmployee
                ? satisfactionWhenOk
                : 0f;
            return Sense(register, cleaner, satisfaction);
        }
    }

    public sealed class FitOutProgress
    {
        public FitOutProgress(
            bool layoutLoaded,
            bool opsAssigned,
            bool capacityMatched,
            bool contractSigned,
            bool cashierHired)
        {
            LayoutLoaded = layoutLoaded;
            OpsAssigned = opsAssigned;
            CapacityMatched = capacityMatched;
            ContractSigned = contractSigned;
            CashierHired = cashierHired;
        }

        public bool LayoutLoaded { get; }

        public bool OpsAssigned { get; }

        public bool CapacityMatched { get; }

        public bool ContractSigned { get; }

        public bool CashierHired { get; }

        public static FitOutProgress Bare()
        {
            return new FitOutProgress(false, false, false, false, false);
        }

        public static FitOutProgress Fitted()
        {
            return new FitOutProgress(true, true, true, true, true);
        }
    }

    public sealed class FitOutPick
    {
        public FitOutPick(string place, string step, bool chosen, string? gate = null)
        {
            Place = place ?? "";
            Step = step ?? "";
            Chosen = chosen;
            Gate = gate ?? "";
        }

        public string Place { get; }

        public string Step { get; }

        public bool Chosen { get; }

        public string Gate { get; }

        public static FitOutPick None()
        {
            return new FitOutPick("", "", false);
        }
    }

    public sealed class FitOutSite
    {
        public FitOutSite(string street, int number, bool bare, FitOutProgress? progress)
        {
            Street = street ?? "";
            Number = number;
            Bare = bare;
            Progress = progress ?? FitOutProgress.Bare();
        }

        public string Street { get; }

        public int Number { get; }

        public bool Bare { get; }

        public FitOutProgress Progress { get; }

        public string Place
        {
            get
            {
                return Street + " " + Number.ToString(CultureInfo.InvariantCulture);
            }
        }
    }

    public static class FitOut
    {
        public static FitOutPick Decide(
            IReadOnlyList<StoreHealth>? priorStores,
            string? place,
            FitOutProgress? bare,
            string? requiredCourse = null)
        {
            var gate = CourseGate.Name(priorStores, place, bare, requiredCourse);
            if (gate.Length > 0)
            {
                return new FitOutPick(place ?? "", "", false, gate);
            }

            if (string.IsNullOrEmpty(place) || bare == null)
            {
                return FitOutPick.None();
            }

            if (!bare.LayoutLoaded
                && (priorStores == null
                    || priorStores.Count == 0
                    || !StoreHealthGate.ExpansionAllowed(priorStores)))
            {
                return FitOutPick.None();
            }

            if (!bare.LayoutLoaded)
            {
                return new FitOutPick(place, "layout", true);
            }

            if (!bare.OpsAssigned)
            {
                return new FitOutPick(place, "ops", true);
            }

            if (!bare.CapacityMatched)
            {
                return new FitOutPick(place, "capacity", true);
            }

            if (!bare.ContractSigned)
            {
                return new FitOutPick(place, "contract", true);
            }

            if (!bare.CashierHired)
            {
                return new FitOutPick(place, "cashier", true);
            }

            return FitOutPick.None();
        }

        public static string PlaceNeedingOps(IReadOnlyList<FitOutSite>? sites)
        {
            if (sites == null)
            {
                return "";
            }

            foreach (var site in sites)
            {
                if (site == null || site.Bare || site.Street.Length == 0 || site.Number <= 0)
                {
                    continue;
                }

                if (site.Progress.LayoutLoaded && !site.Progress.OpsAssigned)
                {
                    return site.Place;
                }
            }

            return "";
        }

        public static FitOutPick BareLot(
            IReadOnlyList<StoreHealth>? owned,
            IReadOnlyList<FitOutSite>? sites,
            string? requiredCourse)
        {
            if (sites == null)
            {
                return FitOutPick.None();
            }

            foreach (var site in sites)
            {
                if (site == null || !site.Bare || site.Street.Length == 0 || site.Number <= 0)
                {
                    continue;
                }

                var priors = new List<StoreHealth>();
                if (owned != null)
                {
                    foreach (var health in owned)
                    {
                        if (health != null && health.Place != site.Place)
                        {
                            priors.Add(health);
                        }
                    }
                }

                var pick = Decide(priors, site.Place, FitOutProgress.Bare(), requiredCourse);
                if (pick.Gate.Length > 0 || pick.Chosen)
                {
                    return pick;
                }
            }

            return FitOutPick.None();
        }

        public static string Line(FitOutPick? pick, int day)
        {
            if (pick == null || !pick.Chosen)
            {
                return "store.fitout version=" + LoadLog.Version
                    + " day=" + day.ToString(CultureInfo.InvariantCulture)
                    + " place= step= chosen=false";
            }

            return "store.fitout version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " place=" + pick.Place
                + " step=" + pick.Step
                + " chosen=true";
        }

        public static string Record(string localAppData, FitOutPick? pick, int day)
        {
            var line = Line(pick, day);
            if (pick == null || !pick.Chosen)
            {
                return line;
            }

            var path = LoadLog.CeoLogPath(localAppData ?? "");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + System.Environment.NewLine);
            return line;
        }
    }
}
