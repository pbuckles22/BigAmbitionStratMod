using System.Collections.Generic;

namespace ChaoticCEO
{
    public sealed class StaffOffer
    {
        public StaffOffer(string kind, string role, string planId, int amount)
        {
            Kind = kind ?? "";
            Role = role ?? "";
            PlanId = planId ?? "";
            Amount = amount;
        }

        public string Kind { get; }

        public string Role { get; }

        public string PlanId { get; }

        public int Amount { get; }
    }

    public sealed class StaffPlan
    {
        public StaffPlan(string role, string kind, string planId, int amount)
        {
            Role = role ?? "";
            Kind = kind ?? "";
            PlanId = planId ?? "";
            Amount = amount;
        }

        public string Role { get; }

        public string Kind { get; }

        public string PlanId { get; }

        public int Amount { get; }
    }

    public static class Staffing
    {
        public static StaffPlan Lock(DeliveryRun? run, IReadOnlyList<StaffOffer>? offers)
        {
            var role = run == null ? "" : run.Family ?? "";
            if (role.Length == 0 || offers == null)
            {
                return new StaffPlan("", "", "", 0);
            }

            StaffOffer? winner = null;
            foreach (var offer in offers)
            {
                if (!Counts(role, offer))
                {
                    continue;
                }

                if (winner == null || Beats(offer, winner))
                {
                    winner = offer;
                }
            }

            if (winner == null)
            {
                return new StaffPlan(role, "", "", 0);
            }

            return new StaffPlan(winner.Role, winner.Kind, winner.PlanId, winner.Amount);
        }

        private static bool Counts(string role, StaffOffer? offer)
        {
            if (offer == null || offer.Role != role || offer.Amount == 0)
            {
                return false;
            }

            return offer.Kind == "hr" || offer.Kind == "headhunter";
        }

        private static bool Beats(StaffOffer offer, StaffOffer winner)
        {
            if (offer.Amount < 0)
            {
                return true;
            }

            if (winner.Amount < 0)
            {
                return false;
            }

            return offer.Amount >= winner.Amount;
        }
    }
}
