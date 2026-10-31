using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class CityBoard
    {
        public CityBoard(
            IReadOnlyList<RoadStep>? steps,
            LogisticsReview? review,
            IReadOnlyList<OverlayRow>? overlay)
        {
            Steps = steps ?? Array.Empty<RoadStep>();
            Review = review ?? LogisticsReview.None();
            Overlay = overlay ?? Array.Empty<OverlayRow>();
        }

        public IReadOnlyList<RoadStep> Steps { get; }

        public LogisticsReview Review { get; }

        public IReadOnlyList<OverlayRow> Overlay { get; }
    }

    public static class Roadshow
    {
        public const int Top = 10;

        public static IReadOnlyList<BuildingAsk> Prices(
            IReadOnlyList<BuildingAsk>? forSale,
            IReadOnlyList<BuildingAsk>? owned)
        {
            var rows = new List<BuildingAsk>();
            var seen = new HashSet<(string Street, int Number)>();
            AddPrices(rows, seen, forSale);
            AddPrices(rows, seen, owned);
            return rows;
        }

        public static CityBoard Compose(
            IReadOnlyList<FinancialDay>? days,
            IReadOnlyList<DeliveryStop>? stops,
            IReadOnlyList<WarehouseHold>? holds,
            IReadOnlyList<BuildingAsk>? asks,
            IReadOnlyList<PortfolioBusiness>? portfolio,
            IReadOnlyList<PortfolioBusiness>? shut = null)
        {
            var ledger = days ?? Array.Empty<FinancialDay>();
            var routes = stops ?? Array.Empty<DeliveryStop>();
            var stock = holds ?? Array.Empty<WarehouseHold>();
            var prices = asks ?? Array.Empty<BuildingAsk>();
            var businesses = portfolio ?? Array.Empty<PortfolioBusiness>();
            var overlay = Overlay.Build(ledger, businesses, stock);
            var api = GameApi.Live();
            var scored = CloseScore.Score(api, ledger);
            var review = Logistics.Review(api, routes, stock);
            var payback = Payback.Decide(api, review, ledger, prices);
            var steps = Roadmap.Order(api, scored, review, payback, ledger, Top);
            return new CityBoard(StillTrading(steps, shut), review, overlay);
        }

        private static IReadOnlyList<RoadStep> StillTrading(
            IReadOnlyList<RoadStep> steps,
            IReadOnlyList<PortfolioBusiness>? shut)
        {
            var closed = new HashSet<(string Street, int Number)>();
            if (shut != null)
            {
                foreach (var business in shut)
                {
                    if (business == null || business.Street.Length == 0 || business.Number <= 0)
                    {
                        continue;
                    }

                    closed.Add((business.Street, business.Number));
                }
            }

            var kept = new List<RoadStep>();
            foreach (var step in steps)
            {
                if (step == null)
                {
                    continue;
                }

                if (step.Kind == "close" && closed.Contains((step.Street, step.Number)))
                {
                    continue;
                }

                kept.Add(step);
            }

            return kept;
        }

        public static IReadOnlyList<PanelLine> Lines(CityBoard? board)
        {
            var lines = new List<PanelLine>();
            if (board == null)
            {
                return lines;
            }

            foreach (var step in board.Steps)
            {
                if (step == null || step.Street.Length == 0 || step.Number <= 0)
                {
                    continue;
                }

                lines.Add(new PanelLine(step.Kind, "step", step.Street, step.Number, step.Explain));
            }

            var move = board.Review.Move;
            if (move != null && move.Destination.Length > 0)
            {
                Split(move.Destination, out var street, out var number);
                lines.Add(new PanelLine("move", "logistics", street, number, move.Explain));
            }

            return lines;
        }

        public static IReadOnlyList<string> Open(string localAppData, CityBoard? board)
        {
            var lines = Lines(board);
            var path = LoadLog.CeoLogPath(localAppData);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var opened = "panel.opened version=" + LoadLog.Version
                + " rows=" + lines.Count.ToString(CultureInfo.InvariantCulture);
            File.AppendAllText(path, opened + Environment.NewLine);
            var shown = Record(localAppData, board);
            var changed = LoadLog.Remember(localAppData, shown);
            var written = new List<string> { opened, shown };
            if (changed.Length > 0)
            {
                written.Add(changed);
            }

            return written;
        }

        public static string Record(string localAppData, CityBoard? board)
        {
            if (board == null)
            {
                return "";
            }

            RoadStep? first = null;
            var count = 0;
            foreach (var step in board.Steps)
            {
                if (step == null || step.Street.Length == 0 || step.Number <= 0)
                {
                    continue;
                }

                if (first == null)
                {
                    first = step;
                }

                count++;
            }

            var move = board.Review.Move;
            var moved = move != null && move.Destination.Length > 0;
            var line = "roadmap.shown version=" + LoadLog.Version
                + " steps=" + count.ToString(CultureInfo.InvariantCulture)
                + " street=" + (first == null ? "" : first.Street)
                + " number=" + (first == null ? 0 : first.Number).ToString(CultureInfo.InvariantCulture)
                + " gain=" + Format(first == null ? 0d : first.DailyGain)
                + " move=" + (moved ? "1" : "0");
            var path = LoadLog.CeoLogPath(localAppData);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + Environment.NewLine);
            return line;
        }

        private static void AddPrices(
            List<BuildingAsk> rows,
            HashSet<(string Street, int Number)> seen,
            IReadOnlyList<BuildingAsk>? asks)
        {
            if (asks == null)
            {
                return;
            }

            foreach (var ask in asks)
            {
                if (ask == null || ask.Street.Length == 0 || ask.Number <= 0 || ask.Price <= 0d)
                {
                    continue;
                }

                var key = (ask.Street, ask.Number);
                if (!seen.Add(key))
                {
                    continue;
                }

                rows.Add(ask);
            }
        }

        private static void Split(string destination, out string street, out int number)
        {
            street = destination ?? "";
            number = 0;
            var split = street.LastIndexOf(' ');
            if (split <= 0)
            {
                return;
            }

            if (!int.TryParse(
                street.Substring(split + 1),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed)
                || parsed <= 0)
            {
                return;
            }

            number = parsed;
            street = street.Substring(0, split);
        }

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
}
