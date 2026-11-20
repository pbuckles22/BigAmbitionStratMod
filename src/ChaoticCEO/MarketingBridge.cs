using System;
using System.Collections.Generic;
using Buildings;
using Entities;
using Helpers;

namespace ChaoticCEO
{
    public static class MarketingBridge
    {
        public static bool HasCampaign(string street, int number)
        {
            var registration = FindStore(street, number);
            if (registration?.marketingCampaigns == null)
            {
                return false;
            }

            foreach (var campaign in registration.marketingCampaigns)
            {
                if (campaign != null && campaign.enabled)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool Start(MarketingStartPick? pick)
        {
            if (pick == null || !pick.Started || pick.Street.Length == 0 || pick.Number <= 0)
            {
                return false;
            }

            var registration = FindStore(pick.Street, pick.Number);
            var agency = FindAgency() ?? registration?.Address;
            if (registration == null || agency == null || !TryType(pick.Type, out var type))
            {
                return false;
            }

            try
            {
                if (registration.marketingCampaigns == null)
                {
                    registration.marketingCampaigns = new List<MarketingCampaign>();
                }

                MarketingCampaign? existing = null;
                foreach (var campaign in registration.marketingCampaigns)
                {
                    if (campaign != null
                        && campaign.agencyAddress == agency
                        && campaign.marketingTypeName == type)
                    {
                        existing = campaign;
                        break;
                    }
                }

                if (existing != null)
                {
                    existing.enabled = true;
                }
                else
                {
                    registration.marketingCampaigns.Add(new MarketingCampaign
                    {
                        agencyAddress = agency,
                        marketingTypeName = type,
                        enabled = true,
                    });
                }

                BusinessHelper.UpdatePromotion(registration);
                SaveGameManager.MarkChange();
                return true;
            }
            catch (Exception ex)
            {
                CrashTrace.Seen(ex, "MarketingBridge.Start.1", "");
                return false;
            }
        }

        public static bool Stop(MarketingStopPick? pick)
        {
            if (pick == null || !pick.Stopped || pick.Street.Length == 0 || pick.Number <= 0)
            {
                return false;
            }

            var registration = FindStore(pick.Street, pick.Number);
            if (registration?.marketingCampaigns == null)
            {
                return false;
            }

            try
            {
                registration.marketingCampaigns.Clear();
                BusinessHelper.UpdatePromotion(registration);
                SaveGameManager.MarkChange();
                return true;
            }
            catch (Exception ex)
            {
                CrashTrace.Seen(ex, "MarketingBridge.Stop.1", "");
                return false;
            }
        }

        private static BuildingRegistration? FindStore(string street, int number)
        {
            var current = SaveGameManager.Current;
            if (current?.BuildingRegistrations == null)
            {
                return null;
            }

            foreach (var registration in current.BuildingRegistrations)
            {
                if (registration?.Address == null
                    || !registration.RentedByPlayer
                    || registration.Address.streetName != street
                    || registration.Address.streetNumber != number)
                {
                    continue;
                }

                return registration;
            }

            return null;
        }

        private static Address? FindAgency()
        {
            var current = SaveGameManager.Current;
            if (current?.BuildingRegistrations == null)
            {
                return null;
            }

            foreach (var registration in current.BuildingRegistrations)
            {
                if (registration?.Address == null || registration.BuildingCached?.SpecialService == null)
                {
                    continue;
                }

                if (registration.BuildingCached.SpecialService.settings is MarketingAgencySettings)
                {
                    return registration.Address;
                }
            }

            return null;
        }

        private static bool TryType(string name, out MarketingTypeName type)
        {
            type = MarketingTypeName.None;
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            try
            {
                type = (MarketingTypeName)Enum.Parse(typeof(MarketingTypeName), name, ignoreCase: false);
                return type != MarketingTypeName.None;
            }
            catch (Exception ex)
            {
                CrashTrace.Seen(ex, "MarketingBridge.TryType.1", "");
                return false;
            }
        }
    }
}
