using System.Collections.Generic;
using BigAmbitions.DayNightCycle;
using Blueprints;
using BusinessLayoutSets;

namespace ChaoticCEO
{
    public static class StarterBridge
    {
        public static bool Inside(BuildingRent rent)
        {
            if (rent == null || !rent.Rented)
            {
                return false;
            }

            var manager = InstanceBehavior<BuildingManager>.Instance;
            var registration = manager == null ? null : manager.buildingRegistration;
            return registration != null
                && registration.StreetName == rent.Street
                && registration.StreetNumber == rent.Number;
        }

        public static IReadOnlyList<StarterOffer> Offers(BuildingRegistration registration)
        {
            var size = new BuildingSizeInfo(registration);
            BusinessLayoutSetHelper.GetAllBusinessLayoutSets();
            var offers = new List<StarterOffer>();
            foreach (var offer in Starter.StageOne())
            {
                if (BusinessLayoutSetHelper.GetOrLoadBusinessLayoutSet(
                    offer.BusinessType,
                    size,
                    offer.Layout,
                    false) != null)
                {
                    offers.Add(offer);
                }
            }

            return offers;
        }

        public static bool Live(BuildingRegistration registration, StarterPick pick)
        {
            if (registration == null || pick == null || !pick.Loaded)
            {
                return false;
            }

            BuildingManager? manager = null;
            var swapped = false;
            BuildingRegistration? previous = null;
            try
            {
                registration.BusinessName = Starter.BusinessName(
                    pick.BusinessType,
                    registration.Neighborhood,
                    registration.StreetName,
                    registration.StreetNumber);
                registration.businessTypeName = pick.BusinessType;
                registration.Layout = pick.Layout;
                registration.scheduleDays = Week(pick.OpenFrom, pick.OpenTo);
                registration.TemporarilyClose(false);
                var current = SaveGameManager.Current;
                if (current != null)
                {
                    registration.creationDay = current.Day;
                }

                manager = InstanceBehavior<BuildingManager>.Instance;
                previous = manager == null ? null : manager.buildingRegistration;
                if (manager != null && previous != registration)
                {
                    manager.buildingRegistration = registration;
                    swapped = true;
                }

                BusinessLayoutSetHelper.LoadBusinessLayoutSet(pick.Layout);
                return TypeLanded(registration, pick);
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "StarterBridge.Live.1", "");
                return TypeLanded(registration, pick);
            }
            finally
            {
                if (swapped && manager != null)
                {
                    manager.buildingRegistration = previous;
                }
            }
        }

        private static bool TypeLanded(BuildingRegistration registration, StarterPick pick)
        {
            return pick.BusinessType.Length > 0
                && pick.BusinessType != "ba:businesstype_empty"
                && registration.businessTypeName == pick.BusinessType;
        }

        private static List<ScheduleDay> Week(int openFrom, int openTo)
        {
            return new List<ScheduleDay>
            {
                Day(DayOfWeekOrdered.Monday, openFrom, openTo, true),
                Day(DayOfWeekOrdered.Tuesday, openFrom, openTo, true),
                Day(DayOfWeekOrdered.Wednesday, openFrom, openTo, true),
                Day(DayOfWeekOrdered.Thursday, openFrom, openTo, true),
                Day(DayOfWeekOrdered.Friday, openFrom, openTo, true),
                Day(DayOfWeekOrdered.Saturday, openFrom, openTo, true),
                Day(DayOfWeekOrdered.Sunday, openFrom, openTo, false),
            };
        }

        private static ScheduleDay Day(DayOfWeekOrdered day, int openFrom, int openTo, bool open)
        {
            return new ScheduleDay
            {
                day = day,
                isOpen = open,
                openingHourSlots = new List<OpeningHourSlot>
                {
                    new OpeningHourSlot(openFrom, openTo),
                },
            };
        }
    }
}
