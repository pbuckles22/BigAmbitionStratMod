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
            catch (System.Exception)
            {
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
            catch (System.Exception)
            {
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
            catch (System.Exception)
            {
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
                    if (HeadcountBridge.Blocks(Cashier.Skill))
                    {
                        var kept = HeadcountBridge.Find(Cashier.Skill);
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
            catch (System.Exception)
            {
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

            if (HeadcountBridge.Blocks(Cashier.Skill))
            {
                var kept = HeadcountBridge.Find(Cashier.Skill);
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
    }
}
