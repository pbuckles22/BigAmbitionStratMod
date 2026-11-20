using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class HandDay
    {
        public HandDay(int day, int onHand)
        {
            Day = day;
            OnHand = onHand;
        }

        public int Day { get; }

        public int OnHand { get; }
    }

    public sealed class OpenFurniture
    {
        public OpenFurniture(
            string? fromStreet,
            int fromNumber,
            string? toStreet,
            int toNumber,
            string? item,
            int deliverDay,
            int price)
        {
            FromStreet = fromStreet ?? "";
            FromNumber = fromNumber;
            ToStreet = toStreet ?? "";
            ToNumber = toNumber;
            Item = item ?? "";
            DeliverDay = deliverDay;
            Price = price;
        }

        public string FromStreet { get; }

        public int FromNumber { get; }

        public string ToStreet { get; }

        public int ToNumber { get; }

        public string Item { get; }

        public int DeliverDay { get; }

        public int Price { get; }
    }

    public sealed class DeliveryLand
    {
        public DeliveryLand(string line, int need, int target)
        {
            Line = line ?? "";
            Need = need;
            Target = target;
        }

        public string Line { get; }

        public int Need { get; }

        public int Target { get; }
    }

    public static class GoodsReconcile
    {
        public const int RiseDays = Wholesaler.DefaultGapDays;

        public const int LargeNeed = 100;

        public const int OwnedPendingCap = 1;

        public const int FurniturePrice = 1200;

        public static bool AllowRack(string? verdict, int owned, int pending)
        {
            if (verdict == "enough")
            {
                return false;
            }

            if (owned < 0)
            {
                owned = 0;
            }

            if (pending < 0)
            {
                pending = 0;
            }

            return owned + pending < OwnedPendingCap;
        }

        public static bool Rose(bool replace, int need, int fromDay, int fromOnHand, IReadOnlyList<HandDay>? later)
        {
            if (!replace || need < LargeNeed || later == null)
            {
                return false;
            }

            foreach (var row in later)
            {
                if (row != null
                    && row.Day > fromDay
                    && row.Day <= fromDay + RiseDays
                    && row.OnHand > fromOnHand)
                {
                    return true;
                }
            }

            return false;
        }

        public static string GoodsLine(
            int day,
            int deliveryDay,
            string? street,
            int number,
            string? item,
            int need,
            int before,
            int after)
        {
            if (day != deliveryDay || day <= 0)
            {
                return "";
            }

            var delta = after - before;
            var name = delta > 0 ? "goods.delivered" : "goods.missed";
            return name + " version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + (street ?? "")
                + " number=" + number.ToString(CultureInfo.InvariantCulture)
                + " item=" + (item ?? "")
                + " need=" + need.ToString(CultureInfo.InvariantCulture)
                + " before=" + before.ToString(CultureInfo.InvariantCulture)
                + " after=" + after.ToString(CultureInfo.InvariantCulture)
                + " delta=" + delta.ToString(CultureInfo.InvariantCulture);
        }

        public static DeliveryLand Land(
            CoverPlan? plan,
            int day,
            int deliveryDay,
            string? street,
            int number,
            int before,
            int after)
        {
            var need = plan == null ? 0 : plan.Need;
            var target = plan == null ? 0 : plan.Target;
            var item = plan == null ? "" : plan.Item;
            return new DeliveryLand(
                GoodsLine(day, deliveryDay, street, number, item, need, before, after),
                need,
                target);
        }

        public static string OpenLine(int day, OpenFurniture? row)
        {
            if (row == null)
            {
                return "";
            }

            return "furniture.open version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " from=" + row.FromStreet
                + " fromNumber=" + row.FromNumber.ToString(CultureInfo.InvariantCulture)
                + " to=" + row.ToStreet
                + " toNumber=" + row.ToNumber.ToString(CultureInfo.InvariantCulture)
                + " item=" + row.Item
                + " deliver=" + row.DeliverDay.ToString(CultureInfo.InvariantCulture)
                + " price=" + row.Price.ToString(CultureInfo.InvariantCulture);
        }

        public static void Attach(
            string? street,
            int number,
            decimal amount,
            IReadOnlyList<OpenFurniture>? open,
            ref int cursor,
            out string streetOut,
            out int numberOut)
        {
            streetOut = street ?? "";
            numberOut = number;
            if (streetOut.Length > 0 || open == null)
            {
                return;
            }

            var price = (int)Math.Round(amount < 0 ? -amount : amount, MidpointRounding.AwayFromZero);
            if (price != FurniturePrice)
            {
                return;
            }

            while (cursor < open.Count)
            {
                var row = open[cursor];
                cursor++;
                if (row != null && row.Price == FurniturePrice && row.ToStreet.Length > 0)
                {
                    streetOut = row.ToStreet;
                    numberOut = row.ToNumber;
                    return;
                }
            }
        }

        public static int NextDelivery(
            bool replace,
            int currentDay,
            int coverNext,
            int scheduledDay,
            int lastReplaceDay,
            bool rose)
        {
            if (!replace)
            {
                return scheduledDay;
            }

            if (rose)
            {
                return coverNext;
            }

            if (lastReplaceDay > 0
                && currentDay < lastReplaceDay + RiseDays
                && scheduledDay > currentDay)
            {
                return scheduledDay;
            }

            return coverNext;
        }

        public static void Record(string? localAppData, string? line)
        {
            if (string.IsNullOrEmpty(localAppData) || string.IsNullOrEmpty(line))
            {
                return;
            }

            var path = LoadLog.CeoLogPath(localAppData);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + Environment.NewLine);
        }

        public static void RecordOnce(string? localAppData, string? line)
        {
            if (string.IsNullOrEmpty(localAppData) || string.IsNullOrEmpty(line))
            {
                return;
            }

            var path = LoadLog.CeoLogPath(localAppData);
            if (File.Exists(path))
            {
                foreach (var existing in File.ReadAllLines(path))
                {
                    if (existing == line)
                    {
                        return;
                    }
                }
            }

            Record(localAppData, line);
        }
    }
}
