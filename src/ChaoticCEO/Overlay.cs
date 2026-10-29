using System;
using System.Collections.Generic;
using System.Linq;

namespace ChaoticCEO
{
    public sealed class OverlayRow
    {
        public OverlayRow(string tone, string business, string street, int number, double profit, string detail)
        {
            Tone = tone ?? "";
            Business = business ?? "";
            Street = street ?? "";
            Number = number;
            Profit = profit;
            Detail = detail ?? "";
        }

        public string Tone { get; }

        public string Business { get; }

        public string Street { get; }

        public int Number { get; }

        public double Profit { get; }

        public string Detail { get; }
    }

    public static class Overlay
    {
        private const int CloseLimit = 10;

        public static IReadOnlyList<OverlayRow> Build(
            IReadOnlyList<FinancialDay> days,
            IReadOnlyList<PortfolioBusiness> portfolio,
            IReadOnlyList<WarehouseHold> holds)
        {
            return FromRank(
                Advisor.Rank(days, portfolio),
                Advisor.WarehouseFamilyMix(holds),
                Advisor.StreetClusters(portfolio));
        }

        public static IReadOnlyList<OverlayRow> FromRank(
            IReadOnlyList<RankedBusiness> ranked,
            IReadOnlyList<WarehouseMix> mixes,
            IReadOnlyList<StreetCluster> clusters)
        {
            return Mark(CloseTail(ranked), mixes, clusters);
        }

        private static IReadOnlyList<RankedBusiness> CloseTail(IReadOnlyList<RankedBusiness> ranked)
        {
            if (ranked == null || ranked.Count <= CloseLimit)
            {
                return ranked ?? Array.Empty<RankedBusiness>();
            }

            return ranked.Skip(ranked.Count - CloseLimit).ToList();
        }

        private static List<OverlayRow> Mark(
            IReadOnlyList<RankedBusiness> ranked,
            IReadOnlyList<WarehouseMix> mixes,
            IReadOnlyList<StreetCluster> clusters)
        {
            var rows = new List<OverlayRow>();
            AppendClose(rows, ranked);
            AppendWarnings(rows, mixes);
            AppendOpens(rows, clusters);
            return rows;
        }

        private static void AppendClose(List<OverlayRow> rows, IReadOnlyList<RankedBusiness> ranked)
        {
            if (ranked == null)
            {
                return;
            }

            foreach (var row in ranked)
            {
                if (row == null)
                {
                    continue;
                }

                rows.Add(new OverlayRow("red", row.Business, row.Street, row.Number, row.Profit, row.Family));
            }
        }

        private static void AppendWarnings(List<OverlayRow> rows, IReadOnlyList<WarehouseMix> mixes)
        {
            if (mixes == null)
            {
                return;
            }

            foreach (var mix in mixes)
            {
                if (mix == null)
                {
                    continue;
                }

                rows.Add(new OverlayRow("warning", mix.Warehouse, mix.Street, mix.Number, 0d, mix.Families));
            }
        }

        private static void AppendOpens(List<OverlayRow> rows, IReadOnlyList<StreetCluster> clusters)
        {
            if (clusters == null)
            {
                return;
            }

            foreach (var cluster in clusters)
            {
                if (cluster == null)
                {
                    continue;
                }

                rows.Add(new OverlayRow("open", cluster.Family, cluster.Street, cluster.Count, 0d, cluster.Family));
            }
        }
    }
}
