using System.Collections.Generic;
using BigAmbitions.Characters.Appearance;
using BigAmbitions.Items;
using Controllers;
using Entities;
using Helpers;
using UnityEngine;

namespace ChaoticCEO
{
    public static class StoreOpsBridge
    {
        public static bool Live(BuildingRegistration registration, StoreOpsPick? pick, out string reason, out StoreOpsCleaner? placed)
        {
            reason = "";
            placed = null;
            if (registration == null || pick == null || !pick.Assigned)
            {
                reason = "empty";
                return false;
            }

            try
            {
                if (pick.Cleaner.Length > 0)
                {
                    if (!Place(registration, "ba:itemname_cleaningstation", "Cleaning", cleaner: true, out reason))
                    {
                        return false;
                    }

                    placed = ReadCleaner(registration);
                }

                if (pick.Guard.Length > 0 && !Place(registration, "ba:itemname_securityguardlocker", "SecurityGuard", cleaner: false, out reason))
                {
                    return false;
                }

                if (pick.Uniforms && !Uniforms(registration, out reason))
                {
                    return false;
                }

                return true;
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "StoreOpsBridge.Live.1", "");
                reason = "exception";
                return false;
            }
        }

        public static bool LiveRemote(BuildingRegistration registration, StoreOpsPick? pick, out string reason, out StoreOpsCleaner? placed)
        {
            reason = "";
            placed = null;
            if (registration == null || pick == null || !pick.Assigned)
            {
                reason = "empty";
                return false;
            }

            try
            {
                var cleaningId = ItemId(registration, "ba:itemname_cleaningstation");
                var guardId = ItemId(registration, "ba:itemname_securityguardlocker");
                var uniformLocker = ItemId(registration, "ba:itemname_uniformlocker").Length > 0;
                var save = SaveGameManager.Current;
                var ready = registration.uniformsBySkill != null && save != null;
                var presets = PresetIds();
                if (pick.Uniforms && StoreOps.UniformPreset(presets, null).Length == 0)
                {
                    var copied = CopyPreset(registration);
                    if (copied.Length > 0)
                    {
                        presets.Add(copied);
                    }
                }

                var days = registration.scheduleDays == null ? 0 : registration.scheduleDays.Count;
                var plan = RemoteOps.Decide(
                    false,
                    pick,
                    cleaningId,
                    guardId,
                    uniformLocker,
                    ready,
                    presets,
                    days,
                    ScheduledId(registration, cleaningId, WorkShiftType.Cleaning),
                    ScheduledId(registration, guardId, WorkShiftType.Default));
                if (!plan.Applied)
                {
                    reason = plan.Reason;
                    return false;
                }

                if (plan.Uniforms && !WriteUniforms(registration, true, out reason))
                {
                    return false;
                }

                if (plan.HireGuard
                    && !HireOnto(registration, "SecurityGuard", false, plan.GuardItemId, WorkShiftType.Default, out reason, out _, out _))
                {
                    return false;
                }

                var cleanerName = "Cleaner";
                var cleanerEmployee = ScheduledId(registration, plan.CleanerItemId, WorkShiftType.Cleaning);
                if (plan.HireCleaner
                    && !HireOnto(registration, "Cleaning", true, plan.CleanerItemId, WorkShiftType.Cleaning, out reason, out cleanerEmployee, out cleanerName))
                {
                    return false;
                }

                if (plan.CleanerItemId.Length > 0 && cleanerEmployee.Length > 0)
                {
                    placed = new StoreOpsCleaner(
                        cleanerEmployee,
                        cleanerName,
                        plan.CleanerItemId,
                        true,
                        false,
                        StoreOps.UnstartedType("Cleaner"));
                }

                return true;
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "StoreOpsBridge.LiveRemote.1", "");
                reason = "exception";
                return false;
            }
        }

        public static StoreFixtures Fixtures(BuildingRegistration registration)
        {
            var names = new List<string>();
            foreach (var item in Items(registration))
            {
                if (item != null && item.itemName != null)
                {
                    names.Add(item.itemName);
                }
            }

            if (registration.itemInstances != null)
            {
                foreach (var pair in registration.itemInstances)
                {
                    if (pair.Value != null && pair.Value.itemName != null)
                    {
                        names.Add(pair.Value.itemName);
                    }
                }
            }

            var fixtures = StoreFixtures.FromNames(names);
            if (!fixtures.UniformLocker && ActiveCount<UniformLockerController>() > 0)
            {
                fixtures = new StoreFixtures(fixtures.CleaningStation, fixtures.GuardLocker, true);
            }

            if (!fixtures.CleaningStation && ActiveCount<CleaningStationController>() > 0)
            {
                fixtures = new StoreFixtures(true, fixtures.GuardLocker, fixtures.UniformLocker);
            }

            return fixtures;
        }

        public static bool HasItems(BuildingRegistration registration)
        {
            if (registration == null)
            {
                return false;
            }

            if (registration.itemInstances != null)
            {
                foreach (var item in registration.itemInstances.Values)
                {
                    if (item != null)
                    {
                        return true;
                    }
                }
            }

            foreach (var item in Items(registration))
            {
                if (item != null)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool HasCleaner(BuildingRegistration registration)
        {
            if (registration == null)
            {
                return false;
            }

            try
            {
                var cleaner = ReadCleaner(registration);
                return cleaner != null
                    && cleaner.EmployeeId.Length > 0
                    && cleaner.Shift;
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "StoreOpsBridge.HasCleaner.1", "");
                return false;
            }
        }

        private static bool Place(
            BuildingRegistration registration,
            string itemName,
            string skill,
            bool cleaner,
            out string reason)
        {
            reason = "";
            EmployeeStationController? station = cleaner
                ? Find<CleaningStationController>(registration, itemName)
                : Find<SecurityGuardLockerController>(registration, itemName);
            if (station == null)
            {
                reason = "nostation";
                return false;
            }

            if (!Started(station))
            {
                reason = "ops-station-not-initialized-frame0";
                return false;
            }

            var hired = station.employeeInstance ?? station.GetAIEmployeeInstance() ?? Create(skill);
            if (hired == null)
            {
                reason = "noemployee";
                return false;
            }

            if (hired.characterData != null && string.IsNullOrEmpty(hired.characterData.name))
            {
                hired.characterData.name = cleaner ? "Cleaner" : "Guard";
            }

            if (station.employeeInstance == null)
            {
                if (cleaner && HeadcountBridge.Blocks(Cleaner.Skill))
                {
                    var kept = HeadcountBridge.Find(Cleaner.Skill);
                    if (kept == null)
                    {
                        reason = "over";
                        return false;
                    }

                    hired = kept;
                }
                else
                {
                    hired.assignedAddress = registration.Address;
                    EmployeeHelper.HireCandidate(hired);
                }
            }

            Schedule(registration, station, hired, cleaner ? WorkShiftType.Cleaning : WorkShiftType.Default);
            var role = new StoreOpsPick(true, cleaner ? "Cleaner" : "", cleaner ? "" : "Guard", false, "", "", "");
            var source = StoreOps.BodySource(
                role,
                Body(station) != null,
                Character(hired) != null,
                station.employee != null && station.employeeInstance == hired);
            if (StoreOps.Outcome(role, source) != "assigned")
            {
                reason = "nocharacter";
                return false;
            }

            if (source == "kept")
            {
                return true;
            }

            var person = source == "station"
                ? Body(station)
                : source == "record"
                    ? Character(hired)
                    : Spawn(station);
            if (person == null)
            {
                reason = "nocharacter";
                return false;
            }

            if (source == "spawn" && hired.characterData != null && person.appearanceSetter != null)
            {
                person.appearanceSetter.SetAppearance(hired.characterData);
            }

            station.AssignEmployee(person, hired);
            return true;
        }

        public static bool Verify(BuildingRegistration registration, out bool shift, out bool person, out string type, out string uniform)
        {
            shift = false;
            person = false;
            type = "";
            uniform = "";
            if (registration.uniformsBySkill != null
                && registration.uniformsBySkill.TryGetValue(StoreOps.CleanerSkill, out var dressed))
            {
                uniform = dressed ?? "";
            }

            var cleaner = ReadCleaner(registration);
            if (cleaner == null)
            {
                return false;
            }

            shift = cleaner.Shift;
            person = cleaner.Person;
            type = cleaner.Type;
            return true;
        }

        private static StoreOpsCleaner? ReadCleaner(BuildingRegistration registration)
        {
            var shift = false;
            try
            {
                var station = Find<CleaningStationController>(registration, "ba:itemname_cleaningstation");
                if (station == null)
                {
                    return null;
                }

                var itemId = station.ItemInstance?.id;
                var employeeId = station.employeeInstance?.id;
                if (registration.scheduleDays != null && !string.IsNullOrEmpty(itemId) && !string.IsNullOrEmpty(employeeId))
                {
                    foreach (var day in registration.scheduleDays)
                    {
                        if (day?.workShifts == null)
                        {
                            continue;
                        }

                        foreach (var listed in day.workShifts)
                        {
                            if (listed != null
                                && listed.itemInstanceId == itemId
                                && listed.employeeId == employeeId
                                && listed.type == WorkShiftType.Cleaning)
                            {
                                shift = true;
                            }
                        }
                    }
                }

                var employee = station.employee;
                return new StoreOpsCleaner(
                    employeeId,
                    station.employeeInstance?.characterData?.name,
                    itemId,
                    shift,
                    employee != null && employee.gameObject.activeInHierarchy,
                    employee == null ? "" : employee.GetType().Name);
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "StoreOpsBridge.ReadCleaner.1", "");
                return null;
            }
        }

        private static bool Started(EmployeeStationController station)
        {
            var field = typeof(EmployeeStationController).GetField(
                "employeeType",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return field == null || field.GetValue(station) != null;
        }

        private static void Schedule(
            BuildingRegistration registration,
            EmployeeStationController station,
            EmployeeInstance employee,
            WorkShiftType kind)
        {
            if (registration.scheduleDays == null
                || station.ItemInstance == null
                || string.IsNullOrEmpty(station.ItemInstance.id)
                || string.IsNullOrEmpty(employee.id))
            {
                return;
            }

            employee.assignedAddress = registration.Address;
            foreach (var day in registration.scheduleDays)
            {
                if (day?.workShifts == null)
                {
                    continue;
                }

                var listed = false;
                foreach (var shift in day.workShifts)
                {
                    if (shift != null
                        && shift.itemInstanceId == station.ItemInstance.id
                        && shift.employeeId == employee.id)
                    {
                        listed = true;
                        break;
                    }
                }

                if (listed)
                {
                    continue;
                }

                day.AddWorkShift(new WorkShift
                {
                    startingHour = 8,
                    endingHour = 16,
                    employeeId = employee.id,
                    itemInstanceId = station.ItemInstance.id,
                    type = kind,
                });
            }

            employee.UpdateWeeklyHoursAndDays(registration.scheduleDays);
            employee.UpdateAssignedWorkStationItems();
        }

        private static ThirdPersonCharacter? Spawn(Component station)
        {
            var person = PrefabHelper.CreatePrefab<ThirdPersonCharacter>(StoreOps.SpawnPrefab, station.transform);
            if (person == null)
            {
                return null;
            }

            person.gameObject.SetActive(true);
            return person;
        }

        private static EmployeeInstance? Create(string skill)
        {
            var id = skill == "Cleaning"
                ? "ba:skill_cleaning"
                : skill == "SecurityGuard"
                    ? "ba:skill_securityguard"
                    : skill;
            var employee = EmployeeHelper.CreateAIEmployeeInstance(id);
            if (employee != null)
            {
                return employee;
            }

            if (id == skill)
            {
                return null;
            }

            return EmployeeHelper.CreateAIEmployeeInstance(skill);
        }

        private static ThirdPersonCharacter? Body(Component station)
        {
            return station.GetComponentInChildren<ThirdPersonCharacter>(true);
        }

        private static bool Uniforms(BuildingRegistration registration, out string reason)
        {
            var locker = Find<UniformLockerController>(registration, "ba:itemname_uniformlocker");
            return WriteUniforms(registration, locker != null, out reason);
        }

        private static bool WriteUniforms(BuildingRegistration registration, bool locker, out string reason)
        {
            var bySkill = registration.uniformsBySkill;
            var save = SaveGameManager.Current;
            reason = StoreOps.UniformReason(locker, bySkill != null && save != null, "-", dressed: true);
            if (reason.Length > 0 || bySkill == null || save == null)
            {
                return false;
            }

            var business = BusinessTypeHelper.GetData(registration);
            var presets = save.employeePresets;
            var ids = new List<string>();
            foreach (var preset in presets ?? new List<EmployeePreset>())
            {
                ids.Add(preset?.id ?? "");
            }

            var presetId = StoreOps.UniformPreset(ids, null);
            if (presetId.Length == 0 && presets != null && business?.uniforms != null && business.uniforms.Count > 0)
            {
                var source = business.uniforms[0];
                var copy = new EmployeePreset
                {
                    name = source.name,
                    maleElements = new List<AppearanceElementData>(source.maleElements),
                    femaleElements = new List<AppearanceElementData>(source.femaleElements),
                };
                presets.Add(copy);
                presetId = copy.id;
            }

            var skills = StoreOps.UniformSkills(
                business?.employeePrimarySkills,
                Buildings.BuildingTypeHelper.GetData(registration)?.requiredBuildingSkills,
                StoreOps.CleanerSkill);
            StoreOps.SeedUniforms(bySkill, skills, presetId);
            reason = StoreOps.UniformReason(
                true,
                true,
                presetId,
                StoreOps.Dressed(registration.uniformsBySkill, StoreOps.CleanerSkill));
            if (reason.Length > 0)
            {
                return false;
            }

            CustomerDemandHelper.ReloadCachedFulfilled(registration.Address);
            var manager = InstanceBehavior<BuildingManager>.Instance;
            foreach (var skill in skills)
            {
                if (manager != null && registration.uniformsBySkill.TryGetValue(skill, out var chosen))
                {
                    manager.onUniformChanged.Invoke(skill, chosen);
                }
            }

            GameEvent.Invoke("ba:gameevent_employeeuniformassigned");
            reason = "";
            return true;
        }

        private static T? Find<T>(BuildingRegistration registration, string itemName) where T : Component
        {
            ItemInstance? match = null;
            foreach (var item in Items(registration))
            {
                if (item != null && item.itemName == itemName)
                {
                    match = item;
                    break;
                }
            }

            if (match == null)
            {
                return OnlyActive<T>();
            }

            T? nearest = null;
            var best = 25f;
            var count = 0;
            T? only = null;
            foreach (var station in Object.FindObjectsOfType<T>())
            {
                if (station == null || !station.gameObject.activeInHierarchy)
                {
                    continue;
                }

                count++;
                only = station;
                var delta = station.transform.position - match.position;
                var dist = delta.sqrMagnitude;
                if (dist < best)
                {
                    best = dist;
                    nearest = station;
                }
            }

            if (nearest != null)
            {
                return nearest;
            }

            return count == 1 ? only : null;
        }

        private static T? OnlyActive<T>() where T : Component
        {
            T? only = null;
            var count = 0;
            foreach (var station in Object.FindObjectsOfType<T>())
            {
                if (station == null || !station.gameObject.activeInHierarchy)
                {
                    continue;
                }

                count++;
                only = station;
            }

            return count == 1 ? only : null;
        }

        private static int ActiveCount<T>() where T : Component
        {
            var count = 0;
            foreach (var station in Object.FindObjectsOfType<T>())
            {
                if (station != null && station.gameObject.activeInHierarchy)
                {
                    count++;
                }
            }

            return count;
        }

        private static List<ItemInstance> Items(BuildingRegistration registration)
        {
            var items = new List<ItemInstance>();
            registration.GetAssignableItems(items);
            return items;
        }

        private static bool HireOnto(
            BuildingRegistration registration,
            string skill,
            bool cleaner,
            string itemId,
            WorkShiftType kind,
            out string reason,
            out string employeeId,
            out string employeeName)
        {
            reason = "";
            employeeId = "";
            employeeName = cleaner ? "Cleaner" : "Guard";
            EmployeeInstance? hired = null;
            var kept = cleaner && HeadcountBridge.Blocks(Cleaner.Skill);
            if (kept)
            {
                hired = HeadcountBridge.Find(Cleaner.Skill);
                if (hired == null)
                {
                    reason = "over";
                    return false;
                }
            }
            else
            {
                hired = Create(skill);
                if (hired == null)
                {
                    reason = "noemployee";
                    return false;
                }

                hired.assignedAddress = registration.Address;
                EmployeeHelper.HireCandidate(hired);
            }

            if (hired.characterData != null)
            {
                if (string.IsNullOrEmpty(hired.characterData.name))
                {
                    hired.characterData.name = employeeName;
                }
                else
                {
                    employeeName = hired.characterData.name;
                }
            }

            ScheduleOnItem(registration, itemId, hired, kind);
            employeeId = hired.id ?? "";
            if (employeeId.Length == 0)
            {
                reason = "noemployee";
                return false;
            }

            return true;
        }

        private static void ScheduleOnItem(
            BuildingRegistration registration,
            string itemId,
            EmployeeInstance employee,
            WorkShiftType kind)
        {
            if (registration.scheduleDays == null
                || string.IsNullOrEmpty(itemId)
                || string.IsNullOrEmpty(employee.id))
            {
                return;
            }

            employee.assignedAddress = registration.Address;
            foreach (var day in registration.scheduleDays)
            {
                if (day?.workShifts == null)
                {
                    continue;
                }

                var listed = false;
                foreach (var shift in day.workShifts)
                {
                    if (shift != null
                        && shift.itemInstanceId == itemId
                        && shift.employeeId == employee.id)
                    {
                        listed = true;
                        break;
                    }
                }

                if (listed)
                {
                    continue;
                }

                day.AddWorkShift(new WorkShift
                {
                    startingHour = RemoteOps.StartHour,
                    endingHour = RemoteOps.EndHour,
                    employeeId = employee.id,
                    itemInstanceId = itemId,
                    type = kind,
                });
            }

            employee.UpdateWeeklyHoursAndDays(registration.scheduleDays);
            employee.UpdateAssignedWorkStationItems();
        }

        private static string ScheduledId(BuildingRegistration registration, string itemId, WorkShiftType kind)
        {
            if (registration.scheduleDays == null || string.IsNullOrEmpty(itemId))
            {
                return "";
            }

            foreach (var day in registration.scheduleDays)
            {
                if (day?.workShifts == null)
                {
                    continue;
                }

                foreach (var shift in day.workShifts)
                {
                    if (shift != null
                        && shift.itemInstanceId == itemId
                        && shift.type == kind
                        && !string.IsNullOrEmpty(shift.employeeId))
                    {
                        return shift.employeeId;
                    }
                }
            }

            return "";
        }

        private static string ItemId(BuildingRegistration registration, string itemName)
        {
            if (registration.itemInstances != null)
            {
                foreach (var item in registration.itemInstances.Values)
                {
                    if (item != null && item.itemName == itemName && !string.IsNullOrEmpty(item.id))
                    {
                        return item.id;
                    }
                }
            }

            foreach (var item in Items(registration))
            {
                if (item != null && item.itemName == itemName && !string.IsNullOrEmpty(item.id))
                {
                    return item.id;
                }
            }

            return "";
        }

        private static List<string> PresetIds()
        {
            var ids = new List<string>();
            var save = SaveGameManager.Current;
            if (save?.employeePresets == null)
            {
                return ids;
            }

            foreach (var preset in save.employeePresets)
            {
                ids.Add(preset?.id ?? "");
            }

            return ids;
        }

        private static string CopyPreset(BuildingRegistration registration)
        {
            var current = SaveGameManager.Current;
            if (current?.employeePresets == null)
            {
                return "";
            }

            var business = BusinessTypeHelper.GetData(registration);
            if (business?.uniforms == null || business.uniforms.Count == 0)
            {
                return "";
            }

            var source = business.uniforms[0];
            var copy = new EmployeePreset
            {
                name = source.name,
                maleElements = new List<AppearanceElementData>(source.maleElements),
                femaleElements = new List<AppearanceElementData>(source.femaleElements),
            };
            current.employeePresets.Add(copy);
            return copy.id ?? "";
        }

        private static ThirdPersonCharacter? Character(EmployeeInstance employee)
        {
            var flags = System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic;
            foreach (var property in employee.GetType().GetProperties(flags))
            {
                if (property.GetValue(employee) is ThirdPersonCharacter named)
                {
                    return named;
                }
            }

            foreach (var field in employee.GetType().GetFields(flags))
            {
                if (field.GetValue(employee) is ThirdPersonCharacter named)
                {
                    return named;
                }
            }

            return null;
        }
    }
}
