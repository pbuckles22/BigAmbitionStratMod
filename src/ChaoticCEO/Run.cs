namespace ChaoticCEO
{
    public sealed class RunFacts
    {
        public RunFacts(
            float energy,
            float hunger,
            float happiness,
            int unmetDemandDays,
            string employeeState,
            bool hasDirt,
            decimal theft,
            decimal sales,
            bool unmetCustomerDemand,
            decimal marketingCost,
            decimal marketingGain,
            decimal taxDue,
            decimal takeoverOffer,
            decimal takeoverDailyProfit)
        {
            Energy = energy;
            Hunger = hunger;
            Happiness = happiness;
            UnmetDemandDays = unmetDemandDays;
            EmployeeState = employeeState ?? "";
            HasDirt = hasDirt;
            Theft = theft;
            Sales = sales;
            UnmetCustomerDemand = unmetCustomerDemand;
            MarketingCost = marketingCost;
            MarketingGain = marketingGain;
            TaxDue = taxDue;
            TakeoverOffer = takeoverOffer;
            TakeoverDailyProfit = takeoverDailyProfit;
        }

        public float Energy { get; }

        public float Hunger { get; }

        public float Happiness { get; }

        public int UnmetDemandDays { get; }

        public string EmployeeState { get; }

        public bool HasDirt { get; }

        public decimal Theft { get; }

        public decimal Sales { get; }

        public bool UnmetCustomerDemand { get; }

        public decimal MarketingCost { get; }

        public decimal MarketingGain { get; }

        public decimal TaxDue { get; }

        public decimal TakeoverOffer { get; }

        public decimal TakeoverDailyProfit { get; }
    }

    public sealed class RunPick
    {
        public RunPick(string action, bool chosen)
        {
            Action = action ?? "";
            Chosen = chosen;
        }

        public string Action { get; }

        public bool Chosen { get; }

        public static RunPick None()
        {
            return new RunPick("", false);
        }
    }

    public static class Run
    {
        public const float NeedFloor = TimeAdvance.NeedFloor;

        public const int DemandDays = StaffDemand.DemandDays;

        public const decimal TheftMax = StoreOps.TheftMax;

        public const decimal MarketingCover = Marketing.MarketingCover;

        public const int PaybackMax = RivalTakeover.PaybackMax;

        public static RunPick Decide(RunFacts? facts)
        {
            if (facts == null)
            {
                return RunPick.None();
            }

            if (facts.Energy <= NeedFloor || facts.Happiness <= NeedFloor)
            {
                return new RunPick("sleep", true);
            }

            if (facts.Hunger <= NeedFloor)
            {
                return new RunPick("eat", true);
            }

            if (facts.UnmetDemandDays >= DemandDays)
            {
                return new RunPick("demand", true);
            }

            if (facts.EmployeeState == "sick"
                || facts.EmployeeState == "retired"
                || facts.EmployeeState == "quitting"
                || facts.EmployeeState == "poached")
            {
                return new RunPick("replace", true);
            }

            if (facts.HasDirt)
            {
                return new RunPick("cleaner", true);
            }

            if (facts.Sales > 0m && facts.Theft > facts.Sales * TheftMax)
            {
                return new RunPick("guard", true);
            }

            if (facts.UnmetCustomerDemand)
            {
                return new RunPick("customer", true);
            }

            if (facts.MarketingGain > 0m
                && facts.MarketingCost > 0m
                && facts.MarketingCost < facts.MarketingGain * MarketingCover)
            {
                return new RunPick("marketing", true);
            }

            if (facts.TaxDue > 0m)
            {
                return new RunPick("tax", true);
            }

            if (facts.TakeoverOffer > 0m
                && facts.TakeoverDailyProfit > 0m
                && facts.TakeoverOffer < facts.TakeoverDailyProfit * PaybackMax)
            {
                return new RunPick("decline", true);
            }

            return RunPick.None();
        }
    }
}
