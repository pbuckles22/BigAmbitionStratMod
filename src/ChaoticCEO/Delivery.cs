using System.IO;

namespace ChaoticCEO
{
    public sealed class StoreDelivery
    {
        public StoreDelivery(
            string destination,
            string from,
            string to,
            string family,
            bool moved,
            string call)
        {
            Destination = destination ?? "";
            From = from ?? "";
            To = to ?? "";
            Family = family ?? "";
            Moved = moved;
            Call = call ?? "";
        }

        public string Destination { get; }

        public string From { get; }

        public string To { get; }

        public string Family { get; }

        public bool Moved { get; }

        public string Call { get; }
    }

    public static class Delivery
    {
        public static StoreDelivery Confirm(
            ApiNote? api,
            LogisticsReview? review,
            string? destination,
            bool confirmed)
        {
            var move = review?.Move;
            if (!confirmed
                || api == null
                || api.Delivery.Length == 0
                || move == null
                || string.IsNullOrEmpty(destination)
                || move.Destination != destination)
            {
                return None();
            }

            return new StoreDelivery(move.Destination, move.From, move.To, move.Family, true, api.Delivery);
        }

        public static string Record(string localAppData, StoreDelivery? redo)
        {
            if (redo == null || !redo.Moved)
            {
                return "";
            }

            var line = "logistics.redone version=" + LoadLog.Version
                + " destination=" + redo.Destination
                + " from=" + redo.From
                + " to=" + redo.To
                + " family=" + redo.Family
                + " call=" + redo.Call;
            var path = LoadLog.CeoLogPath(localAppData);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + System.Environment.NewLine);
            return line;
        }

        public static StoreDelivery Reload(StoreDelivery? redo)
        {
            return None();
        }

        private static StoreDelivery None()
        {
            return new StoreDelivery("", "", "", "", false, "");
        }
    }
}
