namespace ChaoticCEO
{
    public sealed class StageThresholds
    {
        public StageThresholds(
            decimal winNetWorth,
            int simHorizonDays,
            decimal calibrateTol,
            decimal demandNoise,
            decimal rivalNoise,
            decimal hireNoise,
            decimal policyWorstTol,
            decimal stage2Cash,
            int stage2Stores,
            int stage3Supplied,
            decimal stage4Cash,
            decimal truckBusy,
            int clusterMin,
            decimal priceStep,
            decimal priceSatisfied,
            int refillDays,
            int hrSpan,
            int huntSpan,
            int reserveDays,
            decimal daySpend,
            decimal loanCover,
            int paybackMax,
            int fixDays,
            int redDays,
            float needFloor,
            int demandDays,
            decimal theftMax,
            decimal marketingCover,
            int healCap,
            int healStrikeMax)
        {
            WinNetWorth = winNetWorth;
            SimHorizonDays = simHorizonDays;
            CalibrateTol = calibrateTol;
            DemandNoise = demandNoise;
            RivalNoise = rivalNoise;
            HireNoise = hireNoise;
            PolicyWorstTol = policyWorstTol;
            Stage2Cash = stage2Cash;
            Stage2Stores = stage2Stores;
            Stage3Supplied = stage3Supplied;
            Stage4Cash = stage4Cash;
            TruckBusy = truckBusy;
            ClusterMin = clusterMin;
            PriceStep = priceStep;
            PriceSatisfied = priceSatisfied;
            RefillDays = refillDays;
            HrSpan = hrSpan;
            HuntSpan = huntSpan;
            ReserveDays = reserveDays;
            DaySpend = daySpend;
            LoanCover = loanCover;
            PaybackMax = paybackMax;
            FixDays = fixDays;
            RedDays = redDays;
            NeedFloor = needFloor;
            DemandDays = demandDays;
            TheftMax = theftMax;
            MarketingCover = marketingCover;
            HealCap = healCap;
            HealStrikeMax = healStrikeMax;
        }

        public decimal WinNetWorth { get; }

        public int SimHorizonDays { get; }

        public decimal CalibrateTol { get; }

        public decimal DemandNoise { get; }

        public decimal RivalNoise { get; }

        public decimal HireNoise { get; }

        public decimal PolicyWorstTol { get; }

        public decimal Stage2Cash { get; }

        public int Stage2Stores { get; }

        public int Stage3Supplied { get; }

        public decimal Stage4Cash { get; }

        public decimal TruckBusy { get; }

        public int ClusterMin { get; }

        public decimal PriceStep { get; }

        public decimal PriceSatisfied { get; }

        public int RefillDays { get; }

        public int HrSpan { get; }

        public int HuntSpan { get; }

        public int ReserveDays { get; }

        public decimal DaySpend { get; }

        public decimal LoanCover { get; }

        public int PaybackMax { get; }

        public int FixDays { get; }

        public int RedDays { get; }

        public float NeedFloor { get; }

        public int DemandDays { get; }

        public decimal TheftMax { get; }

        public decimal MarketingCover { get; }

        public int HealCap { get; }

        public int HealStrikeMax { get; }

        public static StageThresholds Starting()
        {
            return new StageThresholds(
                TopBar.WinNetWorth,
                Noise.SimHorizonDays,
                Calibrate.Tolerance,
                Noise.DemandNoise,
                Noise.RivalNoise,
                Noise.HireNoise,
                Policy.WorstTol,
                Stage.Stage2Cash,
                Stage.Stage2Stores,
                Stage.Stage3Supplied,
                Stage.Stage4Cash,
                Open.TruckBusy,
                Where.ClusterMin,
                Prices.PriceStep,
                Prices.PriceSatisfied,
                Supply.RefillDays,
                Managers.HrSpan,
                Managers.HuntSpan,
                Money.ReserveDays,
                Money.DaySpend,
                Money.LoanCover,
                RealEstate.PaybackMax,
                Close.FixDays,
                Close.RedDays,
                TimeAdvance.NeedFloor,
                StaffDemand.DemandDays,
                StoreOps.TheftMax,
                Marketing.MarketingCover,
                Audit.HealCap,
                Audit.HealStrikeMax);
        }

        public StageThresholds With(
            decimal? winNetWorth = null,
            int? simHorizonDays = null,
            decimal? calibrateTol = null,
            decimal? demandNoise = null,
            decimal? rivalNoise = null,
            decimal? hireNoise = null,
            decimal? policyWorstTol = null,
            decimal? stage2Cash = null,
            int? stage2Stores = null,
            int? stage3Supplied = null,
            decimal? stage4Cash = null,
            decimal? truckBusy = null,
            int? clusterMin = null,
            decimal? priceStep = null,
            decimal? priceSatisfied = null,
            int? refillDays = null,
            int? hrSpan = null,
            int? huntSpan = null,
            int? reserveDays = null,
            decimal? daySpend = null,
            decimal? loanCover = null,
            int? paybackMax = null,
            int? fixDays = null,
            int? redDays = null,
            float? needFloor = null,
            int? demandDays = null,
            decimal? theftMax = null,
            decimal? marketingCover = null,
            int? healCap = null,
            int? healStrikeMax = null)
        {
            return new StageThresholds(
                winNetWorth ?? WinNetWorth,
                simHorizonDays ?? SimHorizonDays,
                calibrateTol ?? CalibrateTol,
                demandNoise ?? DemandNoise,
                rivalNoise ?? RivalNoise,
                hireNoise ?? HireNoise,
                policyWorstTol ?? PolicyWorstTol,
                stage2Cash ?? Stage2Cash,
                stage2Stores ?? Stage2Stores,
                stage3Supplied ?? Stage3Supplied,
                stage4Cash ?? Stage4Cash,
                truckBusy ?? TruckBusy,
                clusterMin ?? ClusterMin,
                priceStep ?? PriceStep,
                priceSatisfied ?? PriceSatisfied,
                refillDays ?? RefillDays,
                hrSpan ?? HrSpan,
                huntSpan ?? HuntSpan,
                reserveDays ?? ReserveDays,
                daySpend ?? DaySpend,
                loanCover ?? LoanCover,
                paybackMax ?? PaybackMax,
                fixDays ?? FixDays,
                redDays ?? RedDays,
                needFloor ?? NeedFloor,
                demandDays ?? DemandDays,
                theftMax ?? TheftMax,
                marketingCover ?? MarketingCover,
                healCap ?? HealCap,
                healStrikeMax ?? HealStrikeMax);
        }
    }
}
