using System;
using System.Collections.Generic;
using System.Linq;

namespace ChaoticCEO
{
    public sealed class NoiseDraw
    {
        public NoiseDraw(decimal demand, decimal rival, decimal hiring)
        {
            Demand = demand;
            Rival = rival;
            Hiring = hiring;
        }

        public decimal Demand { get; }

        public decimal Rival { get; }

        public decimal Hiring { get; }
    }

    public sealed class NoiseResult
    {
        public NoiseResult(
            int horizonDays,
            IReadOnlyList<decimal> netWorths,
            decimal median,
            decimal worst,
            decimal best)
        {
            HorizonDays = horizonDays;
            NetWorths = netWorths ?? Array.Empty<decimal>();
            Median = median;
            Worst = worst;
            Best = best;
        }

        public int HorizonDays { get; }

        public IReadOnlyList<decimal> NetWorths { get; }

        public int Trials => NetWorths.Count;

        public decimal Median { get; }

        public decimal Worst { get; }

        public decimal Best { get; }

        public static NoiseResult Empty(int horizonDays)
        {
            return new NoiseResult(horizonDays, Array.Empty<decimal>(), 0m, 0m, 0m);
        }
    }

    public static class Noise
    {
        public const int SimHorizonDays = 365;

        public const decimal DemandNoise = 0.10m;

        public const decimal RivalNoise = 0.05m;

        public const decimal HireNoise = 0.10m;

        public static NoiseDraw Draw(int seed, int day)
        {
            var demand = 1m + (2m * Unit(seed, day, 1) - 1m) * DemandNoise;
            var rival = Unit(seed, day, 2) * RivalNoise;
            var hiring = 1m + (2m * Unit(seed, day, 3) - 1m) * HireNoise;
            return new NoiseDraw(demand, rival, hiring);
        }

        public static StatementRow Project(StoreCurve? curve, NoiseDraw? draw)
        {
            if (curve == null || curve.Street.Length == 0)
            {
                return new StatementRow("", 0, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
            }

            var demand = draw == null ? 1m : draw.Demand;
            var rival = draw == null ? 0m : draw.Rival;
            var hiring = draw == null ? 1m : draw.Hiring;
            if (demand < 0m)
            {
                demand = 0m;
            }

            if (rival < 0m)
            {
                rival = 0m;
            }

            if (rival > 1m)
            {
                rival = 1m;
            }

            if (hiring < 0m)
            {
                hiring = 0m;
            }

            var sales = curve.DailySales * demand * (1m - rival);
            var goods = sales * curve.GoodsPerSale;
            var wages = curve.DailyWages * hiring;
            var rent = curve.DailyRent;
            var marketing = curve.DailyMarketing;
            var theft = curve.DailyTheft;
            var profit = sales - goods - wages - rent - marketing - theft;
            return new StatementRow(
                curve.Street,
                curve.Number,
                (float)sales,
                (float)goods,
                (float)wages,
                (float)rent,
                (float)marketing,
                (float)theft,
                (float)profit);
        }

        public static decimal Trial(
            IReadOnlyList<StoreCurve>? curves,
            decimal startNetWorth,
            int seed,
            int horizonDays = SimHorizonDays)
        {
            if (curves == null || curves.Count == 0 || horizonDays <= 0)
            {
                return startNetWorth;
            }

            var netWorth = startNetWorth;
            for (var day = 0; day < horizonDays; day++)
            {
                netWorth += DayProfit(curves, Draw(seed, day));
            }

            return netWorth;
        }

        public static NoiseResult Run(
            IReadOnlyList<StoreCurve>? curves,
            decimal startNetWorth,
            IReadOnlyList<int>? seeds,
            int horizonDays = SimHorizonDays)
        {
            if (curves == null || curves.Count == 0 || seeds == null || seeds.Count == 0 || horizonDays <= 0)
            {
                return NoiseResult.Empty(horizonDays > 0 ? horizonDays : SimHorizonDays);
            }

            var worths = seeds
                .Select(seed => Trial(curves, startNetWorth, seed, horizonDays))
                .OrderBy(worth => worth)
                .ToList();
            var median = worths[worths.Count / 2];
            return new NoiseResult(horizonDays, worths, median, worths[0], worths[worths.Count - 1]);
        }

        private static decimal DayProfit(IReadOnlyList<StoreCurve> curves, NoiseDraw draw)
        {
            decimal profit = 0m;
            foreach (var curve in curves)
            {
                if (curve == null || curve.Street.Length == 0)
                {
                    continue;
                }

                var demand = draw.Demand < 0m ? 0m : draw.Demand;
                var rival = draw.Rival < 0m ? 0m : draw.Rival;
                if (rival > 1m)
                {
                    rival = 1m;
                }

                var hiring = draw.Hiring < 0m ? 0m : draw.Hiring;
                var sales = curve.DailySales * demand * (1m - rival);
                var goods = sales * curve.GoodsPerSale;
                var wages = curve.DailyWages * hiring;
                profit += sales - goods - wages - curve.DailyRent - curve.DailyMarketing - curve.DailyTheft;
            }

            return profit;
        }

        private static decimal Unit(int seed, int day, int channel)
        {
            unchecked
            {
                var x = (uint)seed * 747796405u + (uint)day * 2891336453u + (uint)channel * 2246822519u;
                x ^= x >> 16;
                x *= 2246822519u;
                x ^= x >> 13;
                return (x & 0xFFFFFFu) / 16777216m;
            }
        }
    }
}
