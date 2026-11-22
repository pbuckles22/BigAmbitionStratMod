using System.Collections.Generic;

namespace ChaoticCEO
{
    public sealed class RemoteOpsPlan
    {
        public RemoteOpsPlan(
            bool applied,
            string reason,
            string cleanerItemId,
            string guardItemId,
            bool uniforms,
            string presetId,
            bool hireCleaner,
            bool hireGuard)
        {
            Applied = applied;
            Reason = reason ?? "";
            CleanerItemId = cleanerItemId ?? "";
            GuardItemId = guardItemId ?? "";
            Uniforms = uniforms;
            PresetId = presetId ?? "";
            HireCleaner = hireCleaner;
            HireGuard = hireGuard;
        }

        public bool Applied { get; }

        public string Reason { get; }

        public string CleanerItemId { get; }

        public string GuardItemId { get; }

        public bool Uniforms { get; }

        public string PresetId { get; }

        public bool HireCleaner { get; }

        public bool HireGuard { get; }
    }

    public static class FixtureGate
    {
        public static string Next(bool hasItems, string? layoutName)
        {
            if (hasItems)
            {
                return "ops";
            }

            return string.IsNullOrWhiteSpace(layoutName) ? "no-items" : "apply";
        }
    }

    public static class RemoteOps
    {
        public const int StartHour = 8;

        public const int EndHour = 16;

        public static RemoteOpsPlan Decide(
            bool inScene,
            StoreOpsPick? pick,
            string? cleaningItemId,
            string? guardItemId,
            bool uniformLocker,
            bool uniformSaveReady,
            IEnumerable<string>? presetIds,
            int scheduleDays,
            string? existingCleanerId,
            string? existingGuardId)
        {
            if (inScene)
            {
                return Miss("inscene");
            }

            if (pick == null || !pick.Assigned)
            {
                return Miss("empty");
            }

            var cleanerId = Text(cleaningItemId);
            var guardId = Text(guardItemId);
            var wantsCleaner = pick.Cleaner.Length > 0;
            var wantsGuard = pick.Guard.Length > 0;
            if ((wantsCleaner || wantsGuard) && scheduleDays < 1)
            {
                return Miss("nodays");
            }

            if (wantsCleaner && cleanerId.Length == 0)
            {
                return Miss("nostation");
            }

            if (wantsGuard && guardId.Length == 0)
            {
                return Miss("nostation");
            }

            var preset = "";
            if (pick.Uniforms)
            {
                preset = StoreOps.UniformPreset(presetIds, null);
                var uniformReason = StoreOps.UniformReason(
                    uniformLocker,
                    uniformSaveReady,
                    preset,
                    preset.Length > 0);
                if (uniformReason.Length > 0)
                {
                    return Miss(uniformReason);
                }
            }

            var keptCleaner = Text(existingCleanerId);
            var keptGuard = Text(existingGuardId);
            return new RemoteOpsPlan(
                true,
                "",
                wantsCleaner ? cleanerId : "",
                wantsGuard ? guardId : "",
                pick.Uniforms,
                preset,
                wantsCleaner && keptCleaner.Length == 0,
                wantsGuard && keptGuard.Length == 0);
        }

        public static IReadOnlyList<StaffShift> Shifts(RemoteOpsPlan? plan, string? cleanerEmployeeId)
        {
            var employeeId = Text(cleanerEmployeeId);
            if (plan == null || !plan.Applied || plan.CleanerItemId.Length == 0 || employeeId.Length == 0)
            {
                return new StaffShift[0];
            }

            return new[]
            {
                new StaffShift(employeeId, plan.CleanerItemId, StaffShift.Cleaner),
            };
        }

        public static string ShiftKind(string? itemName, bool cleaningShift)
        {
            if (cleaningShift || itemName == "ba:itemname_cleaningstation")
            {
                return StaffShift.Cleaner;
            }

            if (itemName == "ba:itemname_securityguardlocker")
            {
                return "";
            }

            return StaffShift.Register;
        }

        public static IReadOnlyList<string> UniformSkillList(RemoteOpsPlan? plan)
        {
            if (plan == null || !plan.Applied || !plan.Uniforms)
            {
                return new string[0];
            }

            return StoreOps.UniformSkills(null, null, StoreOps.CleanerSkill);
        }

        public static string Open(
            string? localAppData,
            BuildingRent? rent,
            bool entered,
            RemoteLayoutResult? laid,
            StarterPick? pick,
            StoreFixtures? fixtures,
            StoreOpsFacts? facts,
            int day,
            string? cleaningItemId,
            string? guardItemId = null,
            bool uniformLocker = false,
            bool uniformSaveReady = true,
            IEnumerable<string>? presetIds = null,
            int scheduleDays = 7,
            string? existingCleanerId = null,
            string? existingGuardId = null)
        {
            if (entered || rent == null || !rent.Rented || laid == null || !laid.Applied)
            {
                return "";
            }

            var assigned = StoreOps.Assign(pick, facts, fixtures, GameApi.Live());
            var plan = Decide(
                false,
                assigned,
                cleaningItemId,
                guardItemId,
                uniformLocker,
                uniformSaveReady,
                presetIds,
                scheduleDays,
                existingCleanerId,
                existingGuardId);
            var folder = localAppData ?? "";
            if (!plan.Applied)
            {
                return StoreOps.Miss(folder, assigned, rent, day, plan.Reason);
            }

            if (plan.CleanerItemId.Length > 0)
            {
                var employeeId = Text(existingCleanerId);
                if (employeeId.Length == 0)
                {
                    employeeId = plan.CleanerItemId;
                }

                StoreOps.CleanerLine(
                    folder,
                    rent,
                    day,
                    new StoreOpsCleaner(
                        employeeId,
                        "Cleaner",
                        plan.CleanerItemId,
                        true,
                        false,
                        StoreOps.UnstartedType("Cleaner")));
            }

            return StoreOps.Record(folder, assigned, rent, day);
        }

        private static RemoteOpsPlan Miss(string reason)
        {
            return new RemoteOpsPlan(false, reason, "", "", false, "", false, false);
        }

        private static string Text(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
        }
    }
}
