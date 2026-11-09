using System;
using System.Collections.Generic;

namespace ChaoticCEO
{
    public sealed class PolicyOption
    {
        public PolicyOption(string name, IReadOnlyList<StoreCurve> curves)
        {
            Name = name ?? "";
            Curves = curves ?? Array.Empty<StoreCurve>();
        }

        public string Name { get; }

        public IReadOnlyList<StoreCurve> Curves { get; }
    }

    public sealed class PolicyPick
    {
        public PolicyPick(string name, NoiseResult noise, bool acceptable)
        {
            Name = name ?? "";
            Noise = noise ?? NoiseResult.Empty(ChaoticCEO.Noise.SimHorizonDays);
            Acceptable = acceptable;
        }

        public string Name { get; }

        public NoiseResult Noise { get; }

        public bool Acceptable { get; }

        public decimal Median => Noise.Median;

        public decimal Worst => Noise.Worst;

        public decimal Best => Noise.Best;

        public static PolicyPick None(int horizonDays = ChaoticCEO.Noise.SimHorizonDays)
        {
            return new PolicyPick(
                "",
                NoiseResult.Empty(horizonDays > 0 ? horizonDays : ChaoticCEO.Noise.SimHorizonDays),
                false);
        }
    }

    public static class Policy
    {
        public const decimal WorstTol = 0.98m;

        public static bool Acceptable(decimal worst, decimal median)
        {
            if (median > 0m)
            {
                return worst >= median * WorstTol;
            }

            if (median < 0m)
            {
                return worst >= median * (2m - WorstTol);
            }

            return worst >= 0m;
        }

        public static PolicyPick Compare(
            IReadOnlyList<PolicyOption>? options,
            decimal startNetWorth,
            IReadOnlyList<int>? seeds,
            int horizonDays = ChaoticCEO.Noise.SimHorizonDays)
        {
            if (options == null || options.Count == 0 || seeds == null || seeds.Count == 0 || horizonDays <= 0)
            {
                return PolicyPick.None(horizonDays > 0 ? horizonDays : ChaoticCEO.Noise.SimHorizonDays);
            }

            PolicyPick? winner = null;
            foreach (var option in options)
            {
                if (option == null || option.Name.Length == 0)
                {
                    continue;
                }

                var noise = ChaoticCEO.Noise.Run(option.Curves, startNetWorth, seeds, horizonDays);
                if (noise.Trials == 0)
                {
                    continue;
                }

                if (!Acceptable(noise.Worst, noise.Median))
                {
                    continue;
                }

                if (winner == null || noise.Median > winner.Median)
                {
                    winner = new PolicyPick(option.Name, noise, true);
                }
            }

            return winner ?? PolicyPick.None(horizonDays);
        }
    }
}
