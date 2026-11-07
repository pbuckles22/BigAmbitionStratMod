using System.Collections.Generic;
using BigAmbitions.DayNightCycle;
using Blueprints;
using BusinessLayoutSets;
using Helpers;

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

            try
            {
                registration.BusinessName = pick.Name;
                registration.businessTypeName = pick.BusinessType;
                registration.Layout = pick.Layout;
                registration.scheduleDays = Week(pick.OpenFrom, pick.OpenTo);
                registration.TemporarilyClose(false);
                var current = SaveGameManager.Current;
                if (current != null)
                {
                    registration.creationDay = current.Day;
                }

                BusinessLayoutSetHelper.LoadBusinessLayoutSet(pick.Layout);
                try
                {
                    BusinessHelper.UpdateCustomerCapacity(registration);
                }
                catch (System.Exception)
                {
                }

                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
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
