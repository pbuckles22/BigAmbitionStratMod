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
                    hired.assignedAddress = registration.Address;
                    EmployeeHelper.HireCandidate(hired);
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
    }
}
