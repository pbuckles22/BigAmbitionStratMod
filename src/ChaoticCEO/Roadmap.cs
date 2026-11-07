using System;
using System.Collections.Generic;
using System.Globalization;

namespace ChaoticCEO
{
    public sealed class RoadStep
    {
        public RoadStep(string kind, string street, int number, double dailyGain)
        {
            Kind = kind ?? "";
            Street = street ?? "";
            Number = number;
            DailyGain = dailyGain;
            Explain = Kind == "open"
                ? "Open " + Street
                    + ". Buy " + Street + " " + number.ToString(CultureInfo.InvariantCulture)
                    + ". Daily gain " + Format(dailyGain)
                    + "."
                : "Close " + Street + " "
                    + number.ToString(CultureInfo.InvariantCulture)
                    + ". Daily gain " + Format(dailyGain)
                    + ".";
        }

        public string Kind { get; }

        public string Street { get; }

        public int Number { get; }

        public double DailyGain { get; }

        public string Explain { get; }

        private static string Format(double value)
        {
            var rounded = Math.Round(value, 4, MidpointRounding.ToEven);
            if (rounded == Math.Truncate(rounded))
            {
                return ((long)rounded).ToString(CultureInfo.InvariantCulture);
            }

            return rounded.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }

    public static class Roadmap
    {
        public static IReadOnlyList<RoadStep> Order(
            ApiNote? api,
            IReadOnlyList<CloseScoreLine>? scored,
            LogisticsReview? review,
            RentPayback? payback,
            IReadOnlyList<FinancialDay>? days,
            int top)
        {
            if (api == null
                || api.Build.Length == 0
                || scored == null
                || review == null
                || payback == null
                || days == null
                || top <= 0)
            {
                return Array.Empty<RoadStep>();
            }

            var latest = LatestRows(days);
            var candidates = new List<Candidate>();
            var order = new List<(string Street, int Number)>();
            var index = new Dictionary<(string Street, int Number), int>();
            for (var i = 0; i < scored.Count; i++)
            {
                var line = scored[i];
                if (line == null || line.Street.Length == 0 || line.Number <= 0)
                {
                    continue;
                }

                var key = (line.Street, line.Number);
                if (!index.ContainsKey(key))
                {
                    order.Add(key);
                }

                index[key] = i;
            }

            foreach (var key in order)
            {
                if (!latest.TryGetValue(key, out var row) || row.Profit >= 0d)
                {
                    continue;
                }

                candidates.Add(new Candidate(index[key], "close", key.Street, key.Number, row.Profit));
            }

            if (payback.Bought
                && payback.Street.Length > 0
                && payback.Number > 0
                && latest.TryGetValue((payback.Street, payback.Number), out var rent)
                && rent.Rent > 0d)
            {
                candidates.Add(new Candidate(
                    scored.Count,
                    "open",
                    payback.Street,
                    payback.Number,
                    rent.Rent));
            }

            candidates.Sort(LargerGainThenLater);
            if (top < candidates.Count)
            {
                candidates.RemoveRange(top, candidates.Count - top);
            }

            var steps = new List<RoadStep>(candidates.Count);
            foreach (var candidate in candidates)
            {
                steps.Add(new RoadStep(candidate.Kind, candidate.Street, candidate.Number, candidate.Gain));
            }

            return steps;
        }

        private static int LargerGainThenLater(Candidate left, Candidate right)
        {
            var gain = Rank(right).CompareTo(Rank(left));
            if (gain != 0)
            {
                return gain;
            }

            return right.Index.CompareTo(left.Index);
        }

        private static double Rank(Candidate candidate)
        {
            if (candidate.Kind == "close")
            {
                return -candidate.Gain;
            }

            return candidate.Gain;
        }

        private static Dictionary<(string Street, int Number), (int Day, int Sequence, double Profit, double Rent)> LatestRows(
            IReadOnlyList<FinancialDay> days)
        {
            var best = new Dictionary<(string Street, int Number), (int Day, int Sequence, double Profit, double Rent)>();
            var sequence = 0;
            foreach (var day in days)
            {
                if (day?.Rows == null)
                {
                    continue;
                }

                foreach (var row in day.Rows)
                {
                    sequence++;
                    if (row == null || row.Street.Length == 0)
                    {
                        continue;
                    }

                    var key = (row.Street, row.Number);
                    if (!best.TryGetValue(key, out var current)
                        || day.Day > current.Day
                        || (day.Day == current.Day && sequence > current.Sequence))
                    {
                        best[key] = (day.Day, sequence, row.Profit, row.Rent);
                    }
                }
            }

            return best;
        }

        private sealed class Candidate
        {
            public Candidate(int index, string kind, string street, int number, double gain)
            {
                Index = index;
                Kind = kind;
                Street = street;
                Number = number;
                Gain = gain;
            }

            public int Index { get; }

            public string Kind { get; }

            public string Street { get; }

            public int Number { get; }

            public double Gain { get; }
        }
    }
}
