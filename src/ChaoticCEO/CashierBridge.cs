using System.Collections.Generic;
using BigAmbitions.Items;
using Controllers;
using Entities;
using Helpers;
using UnityEngine;

namespace ChaoticCEO
{
    public static class CashierBridge
    {
        public static bool HasCashier(BuildingRegistration registration)
        {
            if (registration == null)
            {
                return false;
            }

            try
            {
                var station = Station(registration);
                return station != null
                    && station.employeeInstance != null
                    && !string.IsNullOrEmpty(station.employeeInstance.id);
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "CashierBridge.HasCashier.1", "");
                return false;
            }
        }

        public static bool HasRegister(BuildingRegistration registration)
        {
            if (registration?.itemInstances == null)
            {
                return false;
            }

            foreach (var item in registration.itemInstances.Values)
            {
                if (item?.itemName != null
                    && item.itemName.IndexOf("cashregister", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        public static void Departure(
            BuildingRegistration registration,
            out bool resigned,
            out bool retired,
            out bool poached)
        {
            resigned = false;
            retired = false;
            poached = false;
            if (registration?.Address == null)
            {
                return;
            }

            try
            {
                var retiredList = RetiredEmployees();
                if (retiredList != null)
                {
                    foreach (EmployeeInstance employee in retiredList)
                    {
                        if (employee != null && SameAddress(employee.assignedAddress, registration))
                        {
                            retired = true;
                            break;
                        }
                    }
                }

                var resignedList = EmployeeHelper.ResignedEmployeesAndLastRegistrationAssigned;
                if (resignedList != null)
                {
                    foreach (var row in resignedList)
                    {
                        var last = row.Item2;
                        if (last != null
                            && last.StreetName == registration.StreetName
                            && last.StreetNumber == registration.StreetNumber)
                        {
                            resigned = true;
                            break;
                        }
                    }
                }

                var poachedList = registration.poachedEmployees;
                poached = poachedList != null && poachedList.Count > 0;
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "CashierBridge.Departure.1", "");
                resigned = false;
                retired = false;
                poached = false;
            }
        }

        public static bool Fill(BuildingRegistration registration, CashierPick? pick, out string reason)
        {
            reason = "";
            if (registration == null || pick == null || !pick.Hired)
            {
                reason = "empty";
                return false;
            }

            try
            {
                var existing = RegisterEmployee(registration);
                var station = Station(registration);
                if (existing != null && station == null)
                {
                    return true;
                }

                if (existing != null && station != null)
                {
                    if (station.employeeInstance != null
                        && !string.IsNullOrEmpty(station.employeeInstance.id))
                    {
                        return true;
                    }

                    return AssignExisting(station, existing, out reason);
                }

                if (station != null)
                {
                    return Live(registration, pick, out reason);
                }

                return ScheduleOnItem(registration, pick, out reason);
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "CashierBridge.Fill.1", "");
                reason = "exception";
                return false;
            }
        }

        public static bool Live(BuildingRegistration registration, CashierPick? pick, out string reason)
        {
            reason = "";
            if (registration == null || pick == null || !pick.Hired)
            {
                reason = "empty";
                return false;
            }

            try
            {
                var station = Station(registration);
                if (station == null)
                {
                    reason = "nostation";
                    return false;
                }

                EnsureEmployeeType(station);
                if (!Started(station))
                {
                    reason = "notstarted";
                    return false;
                }

                if (station.employeeInstance != null && station.employee != null)
                {
                    return true;
                }

                var hired = station.employeeInstance
                    ?? station.GetAIEmployeeInstance()
                    ?? EmployeeHelper.CreateAIEmployeeInstance(Cashier.Skill);
                if (hired == null)
                {
                    reason = "noemployee";
                    return false;
                }

                if (hired.characterData != null && string.IsNullOrEmpty(hired.characterData.name))
                {
                    hired.characterData.name = Cashier.RoleName;
                }

                if (station.employeeInstance == null)
                {
                    hired = TakeCashier(registration, hired, out reason);
                    if (hired == null)
                    {
                        return false;
                    }
                }

                Schedule(registration, station, hired);
                var person = Body(station) ?? Spawn(station);
                if (person == null)
                {
                    reason = "nocharacter";
                    return false;
                }

                if (hired.characterData != null && person.appearanceSetter != null)
                {
                    person.appearanceSetter.SetAppearance(hired.characterData);
                }

                EnsureBodyType(person, station);
                station.AssignEmployee(person, hired);
                return station.employeeInstance != null;
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "CashierBridge.Live.1", "");
                reason = "exception";
                return false;
            }
        }

        public static bool Scheduled(BuildingRegistration registration)
        {
            if (registration == null)
            {
                return false;
            }

            try
            {
                var read = ReadSchedule(registration);
                var pick = new CashierPick(
                    true,
                    Cashier.RoleName,
                    Cashier.Skill,
                    Cashier.GiftType,
                    registration.StreetName ?? "",
                    registration.StreetNumber,
                    "hire",
                    "cashier");
                var plan = RemoteCashier.Decide(false, pick, read.Registers, read.OpenDays, read.Posted, read.LiveIds);
                return plan.Applied && !plan.Hire && plan.EmployeeId.Length > 0;
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "CashierBridge.Scheduled.1", "");
                return false;
            }
        }

        public static bool LiveRemote(
            BuildingRegistration registration,
            CashierPick? pick,
            out string reason,
            out RemoteCashierPlan? plan,
            out string employeeId)
        {
            reason = "";
            plan = null;
            employeeId = "";
            if (registration == null || pick == null || !pick.Hired)
            {
                reason = "empty";
                return false;
            }

            try
            {
                var read = ReadSchedule(registration);
                plan = RemoteCashier.Decide(false, pick, read.Registers, read.OpenDays, read.Posted, read.LiveIds);
                if (!plan.Applied)
                {
                    reason = plan.Reason;
                    return false;
                }

                Purge(registration, plan);
                var hired = Resolve(registration, plan, out reason);
                if (hired == null || string.IsNullOrEmpty(hired.id))
                {
                    if (reason.Length == 0)
                    {
                        reason = "noemployee";
                    }

                    return false;
                }

                employeeId = hired.id;
                WriteOpenDays(registration, plan, hired);
                return true;
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "CashierBridge.LiveRemote.1", "");
                reason = "exception";
                return false;
            }
        }

        private static CashRegisterController? Station(BuildingRegistration registration)
        {
            if (registration?.Address == null)
            {
                return null;
            }

            CashRegisterController? match = null;
            CashRegisterController? only = null;
            var count = 0;
            foreach (var station in Object.FindObjectsOfType<CashRegisterController>())
            {
                if (station == null || !station.gameObject.activeInHierarchy)
                {
                    continue;
                }

                count++;
                only = station;
                var owned = station.BuildingContext?.Registration;
                if (owned?.Address == null)
                {
                    continue;
                }

                if (owned.Address.streetName == registration.Address.streetName
                    && owned.Address.streetNumber == registration.Address.streetNumber)
                {
                    match = station;
                }
            }

            if (match != null)
            {
                return match;
            }

            return count == 1 ? only : null;
        }

        private static void EnsureEmployeeType(CashRegisterController station)
        {
            if (Started(station))
            {
                return;
            }

            var set = typeof(CashRegisterController).GetMethod(
                "SetEmployeeType",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            set?.Invoke(station, null);
            if (Started(station))
            {
                return;
            }

            var field = typeof(EmployeeStationController).GetField(
                "employeeType",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field == null)
            {
                return;
            }

            var customer = station.BuildingContext?.BusinessType?.customerType.ToString();
            var name = Cashier.RegisterType(customer);
            var type = System.Type.GetType(name)
                ?? typeof(SelfServiceEmployee).Assembly.GetType(name)
                ?? typeof(SelfServiceEmployee);
            field.SetValue(station, type);
        }

        private static void EnsureBodyType(ThirdPersonCharacter person, CashRegisterController station)
        {
            var customer = station.BuildingContext?.BusinessType?.customerType.ToString();
            var name = Cashier.RegisterType(customer);
            if (person.GetComponent(name) != null)
            {
                return;
            }

            var type = System.Type.GetType(name)
                ?? typeof(SelfServiceEmployee).Assembly.GetType(name)
                ?? typeof(SelfServiceEmployee);
            person.gameObject.AddComponent(type);
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
            EmployeeInstance employee)
        {
            if (registration.scheduleDays == null
                || station.ItemInstance == null
                || string.IsNullOrEmpty(station.ItemInstance.id)
                || string.IsNullOrEmpty(employee.id))
            {
                return;
            }

            employee.assignedAddress = registration.Address;
            WriteShifts(registration, station.ItemInstance.id, employee);
        }

        private static void WriteShifts(
            BuildingRegistration registration,
            string itemId,
            EmployeeInstance employee)
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
                    startingHour = 8,
                    endingHour = 16,
                    employeeId = employee.id,
                    itemInstanceId = itemId,
                    type = WorkShiftType.Default,
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

        private static ThirdPersonCharacter? Body(Component station)
        {
            return station.GetComponentInChildren<ThirdPersonCharacter>(true);
        }

        private static bool ScheduleOnItem(BuildingRegistration registration, CashierPick pick, out string reason)
        {
            reason = "";
            var item = RegisterItem(registration);
            if (item == null || string.IsNullOrEmpty(item.id))
            {
                reason = "nostation";
                return false;
            }

            var hired = EmployeeHelper.CreateAIEmployeeInstance(Cashier.Skill);
            if (hired == null)
            {
                reason = "noemployee";
                return false;
            }

            if (hired.characterData != null && string.IsNullOrEmpty(hired.characterData.name))
            {
                hired.characterData.name = Cashier.RoleName;
            }

            hired = TakeCashier(registration, hired, out reason);
            if (hired == null)
            {
                return false;
            }

            WriteShifts(registration, item.id, hired);
            _ = pick;
            return !string.IsNullOrEmpty(hired.id);
        }

        private static bool AssignExisting(
            CashRegisterController station,
            EmployeeInstance employee,
            out string reason)
        {
            reason = "";
            EnsureEmployeeType(station);
            if (!Started(station))
            {
                reason = "notstarted";
                return false;
            }

            var person = Body(station) ?? Spawn(station);
            if (person == null)
            {
                reason = "nocharacter";
                return false;
            }

            if (employee.characterData != null && person.appearanceSetter != null)
            {
                person.appearanceSetter.SetAppearance(employee.characterData);
            }

            EnsureBodyType(person, station);
            station.AssignEmployee(person, employee);
            return station.employeeInstance != null;
        }

        private static EmployeeInstance? RegisterEmployee(BuildingRegistration registration)
        {
            if (registration?.scheduleDays == null)
            {
                return null;
            }

            var current = SaveGameManager.Current;
            if (current?.EmployeeInstances == null)
            {
                return null;
            }

            foreach (var day in registration.scheduleDays)
            {
                if (day?.workShifts == null)
                {
                    continue;
                }

                foreach (var shift in day.workShifts)
                {
                    if (shift == null
                        || string.IsNullOrEmpty(shift.employeeId)
                        || shift.type == WorkShiftType.Cleaning)
                    {
                        continue;
                    }

                    foreach (var employee in current.EmployeeInstances)
                    {
                        if (employee != null
                            && employee.id == shift.employeeId
                            && !employee.poached)
                        {
                            return employee;
                        }
                    }
                }
            }

            return null;
        }

        private static ItemInstance? RegisterItem(BuildingRegistration registration)
        {
            if (registration?.itemInstances == null)
            {
                return null;
            }

            foreach (var item in registration.itemInstances.Values)
            {
                if (item?.itemName != null
                    && item.itemName.IndexOf("cashregister", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return item;
                }
            }

            return null;
        }

        private static System.Collections.IEnumerable? RetiredEmployees()
        {
            var field = typeof(EmployeeHelper).GetField(
                "RetiredEmployees",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(null) as System.Collections.IEnumerable;
        }

        private static bool SameAddress(Address? address, BuildingRegistration registration)
        {
            return address != null
                && registration.Address != null
                && address.streetName == registration.Address.streetName
                && address.streetNumber == registration.Address.streetNumber;
        }

        private sealed class ScheduleRead
        {
            public ScheduleRead(
                List<ServiceRegister> registers,
                List<bool> openDays,
                List<PostedShift> posted,
                List<string> liveIds,
                HashSet<string> knownItems)
            {
                Registers = registers;
                OpenDays = openDays;
                Posted = posted;
                LiveIds = liveIds;
                KnownItems = knownItems;
            }

            public List<ServiceRegister> Registers { get; }

            public List<bool> OpenDays { get; }

            public List<PostedShift> Posted { get; }

            public List<string> LiveIds { get; }

            public HashSet<string> KnownItems { get; }
        }

        private static ScheduleRead ReadSchedule(BuildingRegistration registration)
        {
            var registers = new List<ServiceRegister>();
            var known = new HashSet<string>(System.StringComparer.Ordinal);
            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var item in RegisterItems(registration))
            {
                if (item == null || string.IsNullOrEmpty(item.id) || !known.Add(item.id))
                {
                    continue;
                }

                if (ServesCustomers(item) && seen.Add(item.id))
                {
                    registers.Add(new ServiceRegister(item.id, Cashier.Skill));
                }
            }

            var openDays = new List<bool>();
            var posted = new List<PostedShift>();
            var service = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var register in registers)
            {
                service.Add(register.Id);
            }

            if (registration.scheduleDays != null)
            {
                for (var day = 0; day < registration.scheduleDays.Count; day++)
                {
                    var row = registration.scheduleDays[day];
                    openDays.Add(row != null && row.isOpen);
                    if (row?.workShifts == null)
                    {
                        continue;
                    }

                    foreach (var shift in row.workShifts)
                    {
                        if (shift == null || string.IsNullOrEmpty(shift.itemInstanceId))
                        {
                            continue;
                        }

                        var onRegister = service.Contains(shift.itemInstanceId);
                        var orphan = !onRegister
                            && shift.type == WorkShiftType.Default
                            && !known.Contains(shift.itemInstanceId);
                        if (!onRegister && !orphan)
                        {
                            continue;
                        }

                        posted.Add(new PostedShift(day, shift.employeeId, shift.itemInstanceId, orphan));
                    }
                }
            }

            return new ScheduleRead(registers, openDays, posted, LiveEmployees(registration), known);
        }

        private static List<ItemInstance> RegisterItems(BuildingRegistration registration)
        {
            var items = new List<ItemInstance>();
            if (registration.itemInstances != null)
            {
                foreach (var item in registration.itemInstances.Values)
                {
                    if (item != null)
                    {
                        items.Add(item);
                    }
                }
            }

            var assignable = new List<ItemInstance>();
            registration.GetAssignableItems(assignable);
            items.AddRange(assignable);
            return items;
        }

        private static bool ServesCustomers(ItemInstance item)
        {
            var skills = item.ItemCached != null ? item.ItemCached.suitableSkills : null;
            if (skills == null && !string.IsNullOrEmpty(item.itemName))
            {
                var named = ItemsGetter.GetByName(item.itemName);
                skills = named == null ? null : named.suitableSkills;
            }

            if (skills == null)
            {
                return item.itemName != null
                    && item.itemName.IndexOf("cashregister", System.StringComparison.OrdinalIgnoreCase) >= 0;
            }

            foreach (var skill in skills)
            {
                if (skill == Cashier.Skill)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<string> LiveEmployees(BuildingRegistration registration)
        {
            var ids = new List<string>();
            var current = SaveGameManager.Current;
            if (current?.EmployeeInstances == null)
            {
                return ids;
            }

            foreach (var employee in current.EmployeeInstances)
            {
                if (employee != null
                    && !string.IsNullOrEmpty(employee.id)
                    && !employee.poached
                    && SameAddress(employee.assignedAddress, registration))
                {
                    ids.Add(employee.id);
                }
            }

            return ids;
        }

        private static void Purge(BuildingRegistration registration, RemoteCashierPlan plan)
        {
            if (registration.scheduleDays == null || plan.Stale.Count == 0)
            {
                return;
            }

            for (var day = 0; day < registration.scheduleDays.Count; day++)
            {
                var row = registration.scheduleDays[day];
                if (row?.workShifts == null)
                {
                    continue;
                }

                var drop = new List<WorkShift>();
                foreach (var shift in row.workShifts)
                {
                    if (shift != null && Matches(plan.Stale, day, shift))
                    {
                        drop.Add(shift);
                    }
                }

                foreach (var shift in drop)
                {
                    row.RemoveWorkShift(shift);
                }
            }
        }

        private static bool Matches(IReadOnlyList<PostedShift> stale, int day, WorkShift shift)
        {
            foreach (var row in stale)
            {
                if (row.DayIndex == day
                    && row.EmployeeId == (shift.employeeId ?? "")
                    && row.ItemId == (shift.itemInstanceId ?? ""))
                {
                    return true;
                }
            }

            return false;
        }

        private static EmployeeInstance? Resolve(BuildingRegistration registration, RemoteCashierPlan plan, out string reason)
        {
            reason = "";
            if (plan.EmployeeId.Length > 0)
            {
                var kept = FindEmployee(plan.EmployeeId);
                if (kept != null && SameAddress(kept.assignedAddress, registration))
                {
                    return kept;
                }
            }

            var hired = EmployeeHelper.CreateAIEmployeeInstance(Cashier.Skill);
            if (hired == null)
            {
                reason = "noemployee";
                return null;
            }

            if (hired.characterData != null && string.IsNullOrEmpty(hired.characterData.name))
            {
                hired.characterData.name = Cashier.RoleName;
            }

            return TakeCashier(registration, hired, out reason);
        }

        private static EmployeeInstance? TakeCashier(
            BuildingRegistration registration,
            EmployeeInstance created,
            out string reason)
        {
            reason = "";
            if (!RemoteCashier.Recruit(true, CashiersHere(registration)))
            {
                var kept = CashierHere(registration);
                if (kept == null)
                {
                    reason = "over";
                    return null;
                }

                return kept;
            }

            created.assignedAddress = registration.Address;
            EmployeeHelper.HireCandidate(created);
            if (string.IsNullOrEmpty(created.id) || FindEmployee(created.id) == null)
            {
                reason = "roster";
                return null;
            }

            return created;
        }

        private static int CashiersHere(BuildingRegistration registration)
        {
            var count = 0;
            foreach (var employee in Roster())
            {
                if (SameAddress(employee.assignedAddress, registration) && HasCashierSkill(employee))
                {
                    count++;
                }
            }

            return count;
        }

        private static EmployeeInstance? CashierHere(BuildingRegistration registration)
        {
            foreach (var employee in Roster())
            {
                if (SameAddress(employee.assignedAddress, registration) && HasCashierSkill(employee))
                {
                    return employee;
                }
            }

            return null;
        }

        private static List<EmployeeInstance> Roster()
        {
            var rows = new List<EmployeeInstance>();
            var current = SaveGameManager.Current;
            if (current?.EmployeeInstances == null)
            {
                return rows;
            }

            foreach (var employee in current.EmployeeInstances)
            {
                if (employee != null && !string.IsNullOrEmpty(employee.id) && !employee.poached)
                {
                    rows.Add(employee);
                }
            }

            return rows;
        }

        private static bool HasCashierSkill(EmployeeInstance employee)
        {
            try
            {
                return employee.HasSkill(Cashier.Skill);
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "CashierBridge.HasCashierSkill.1", "");
                return false;
            }
        }

        private static EmployeeInstance? FindEmployee(string id)
        {
            var current = SaveGameManager.Current;
            if (current?.EmployeeInstances == null)
            {
                return null;
            }

            foreach (var employee in current.EmployeeInstances)
            {
                if (employee != null && employee.id == id && !employee.poached)
                {
                    return employee;
                }
            }

            return null;
        }

        private static void WriteOpenDays(BuildingRegistration registration, RemoteCashierPlan plan, EmployeeInstance employee)
        {
            if (registration.scheduleDays == null || string.IsNullOrEmpty(employee.id))
            {
                return;
            }

            employee.assignedAddress = registration.Address;
            foreach (var day in plan.ShiftDays)
            {
                if (day < 0 || day >= registration.scheduleDays.Count)
                {
                    continue;
                }

                var row = registration.scheduleDays[day];
                if (row?.workShifts == null || !row.isOpen)
                {
                    continue;
                }

                foreach (var register in plan.RegisterIds)
                {
                    if (Listed(row, register, employee.id))
                    {
                        continue;
                    }

                    row.AddWorkShift(new WorkShift
                    {
                        startingHour = RemoteCashier.StartHour,
                        endingHour = RemoteCashier.EndHour,
                        employeeId = employee.id,
                        itemInstanceId = register,
                        type = WorkShiftType.Default,
                    });
                }
            }

            employee.UpdateWeeklyHoursAndDays(registration.scheduleDays);
            employee.UpdateAssignedWorkStationItems();
        }

        private static bool Listed(ScheduleDay day, string itemId, string employeeId)
        {
            foreach (var shift in day.workShifts)
            {
                if (shift != null && shift.itemInstanceId == itemId && shift.employeeId == employeeId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
