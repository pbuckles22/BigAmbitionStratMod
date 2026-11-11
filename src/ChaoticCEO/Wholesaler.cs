using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class WholesalerOffer
    {
        public WholesalerOffer(string businessType, string wholesale)
        {
            BusinessType = businessType ?? "";
            Wholesale = wholesale ?? "";
        }

        public string BusinessType { get; }

        public string Wholesale { get; }
    }

    public sealed class WholesalerPick
    {
        public WholesalerPick(string wholesale, string street, int number, bool signed, string call)
        {
            Wholesale = wholesale ?? "";
            Street = street ?? "";
            Number = number;
            Signed = signed;
            Call = call ?? "";
        }

        public string Wholesale { get; }

        public string Street { get; }

        public int Number { get; }

        public bool Signed { get; }

        public string Call { get; }
    }

    public static class Wholesaler
    {
        /// <summary>Buffer days of stock when next delivery is pulled to tomorrow.</summary>
        public const int CoverOpenDays = 2;

        public const int MinContractAmount = 50;

        /// <summary>Live day-24: amount=70 sold through in about one open day.</summary>
        public const int StageOneDailyBurn = 70;

        public const string PaperBag = "ba:itemname_paperbag";

        /// <summary>One-day opening guess before any open day of sales. First drop is this times 1.5.</summary>
        public const int OpeningDailyGuess = 25;

        public const int PaperBagSeed = 50;

        public const int MinBurnFloor = 5;

        public const int MinLineAmount = 10;

        /// <summary>Pull check only. The order line does not multiply by this.</summary>
        public const decimal CoverMultiplier = 1.5m;

        /// <summary>Applied to the daily average only. Not applied to the stand floor, and not multiplied by the window.</summary>
        public const decimal SalesWindowMultiplier = 2.0m;

        public const int MaxKeptWindow = 3;

        public const int DefaultGapDays = 2;

        /// <summary>Units one selling stand holds. Two stands are the retail floor of 200.</summary>
        public const int StandCap = 100;

        /// <summary>Units one cash register holds. That capacity is the paper-bag floor.</summary>
        public const int RegisterCap = 200;

        public static int ContractAmount(int dailyBurn)
        {
            if (dailyBurn <= 0)
            {
                return MinContractAmount;
            }

            var sized = dailyBurn * CoverOpenDays;
            return sized < MinContractAmount ? MinContractAmount : sized;
        }

        public static int ItemAmount(string? item, int dailyBurn)
        {
            var amount = ContractAmount(dailyBurn);
            if (item == PaperBag)
            {
                return amount * 2;
            }

            return amount;
        }

        public static bool MidWeekSurvives(int onHand, int dailyBurn, int openDaysUntilNext)
        {
            if (dailyBurn <= 0)
            {
                return true;
            }

            if (openDaysUntilNext <= 0)
            {
                return onHand > 0;
            }

            return onHand >= dailyBurn * openDaysUntilNext;
        }

        public static bool NeedsReschedule(int currentDay, int nextDeliveryDay)
        {
            return nextDeliveryDay > currentDay + 1;
        }

        public static int RescheduleDay(int currentDay)
        {
            return currentDay + 1;
        }

        public static int RetailStands(string? item)
        {
            return item == PaperBag ? 1 : 2;
        }

        public static int StandFloor(int stands)
        {
            if (stands <= 1)
            {
                return 0;
            }

            return stands * StandCap;
        }

        public static int RegisterFloor(int registers)
        {
            if (registers <= 0)
            {
                return 0;
            }

            return registers * RegisterCap;
        }

        public static CoverPlan Cover(
            string? item,
            int currentDay,
            int nextDeliveryDay,
            IReadOnlyList<CoverSale>? days,
            int onHand = 0,
            int fixtureCap = 0,
            int stands = 1)
        {
            var gap = nextDeliveryDay - currentDay;
            var stock = onHand < 0 ? 0 : onHand;

            var openDays = new List<int>();
            var soldByDay = new Dictionary<int, int>();
            var ordersByDay = new Dictionary<int, int>();
            if (days != null)
            {
                foreach (var day in days)
                {
                    if (day == null || !day.Open || day.Day <= 0 || day.Day >= currentDay)
                    {
                        continue;
                    }

                    if (!soldByDay.ContainsKey(day.Day))
                    {
                        soldByDay[day.Day] = 0;
                        ordersByDay[day.Day] = 0;
                        openDays.Add(day.Day);
                    }

                    soldByDay[day.Day] += day.Sold;
                    ordersByDay[day.Day] += day.StoreOrders;
                }
            }

            openDays.Sort();
            var name = item ?? "";
            var guess = openDays.Count == 0;
            var samples = guess ? 0 : openDays.Count == 1 ? 1 : 2;
            var older = 0;
            var newer = 0;
            decimal average;
            if (guess)
            {
                average = name == PaperBag ? PaperBagSeed : OpeningDailyGuess;
            }
            else
            {
                if (samples == 1)
                {
                    newer = soldByDay[openDays[0]];
                }
                else
                {
                    older = soldByDay[openDays[openDays.Count - 2]];
                    newer = soldByDay[openDays[openDays.Count - 1]];
                }

                if (name == PaperBag && older + newer == 0)
                {
                    if (samples == 1)
                    {
                        newer = ordersByDay[openDays[0]];
                    }
                    else
                    {
                        older = ordersByDay[openDays[openDays.Count - 2]];
                        newer = ordersByDay[openDays[openDays.Count - 1]];
                    }
                }

                if (samples == 2)
                {
                    var low = older < newer ? older : newer;
                    var high = older < newer ? newer : older;
                    if (high >= MinBurnFloor && low < MinBurnFloor)
                    {
                        older = 0;
                        newer = high;
                        samples = 1;
                    }
                }

                average = samples == 1 ? newer : (older + newer) / 2m;
                var burn = CeilUnits(average);
                if (burn < MinBurnFloor)
                {
                    burn = MinBurnFloor;
                }

                average = burn;
            }

            var daysUntilTruck = gap > 0 ? gap : 1;
            var requiredBeforeTruck = CeilUnits(average * daysUntilTruck * CoverMultiplier);
            var runsOut = !guess && gap > 1 && stock < requiredBeforeTruck;
            int window;
            int next;
            bool replace;
            if (gap > MaxKeptWindow || runsOut)
            {
                window = DefaultGapDays;
                replace = true;
                next = RescheduleDay(currentDay);
            }
            else if (gap >= 1)
            {
                window = gap < DefaultGapDays ? DefaultGapDays : gap;
                replace = false;
                next = nextDeliveryDay;
            }
            else if (gap == 0)
            {
                window = DefaultGapDays;
                replace = false;
                next = currentDay;
            }
            else
            {
                window = DefaultGapDays;
                replace = true;
                next = currentDay;
            }

            var salesTarget = CeilUnits(average * SalesWindowMultiplier);
            var floor = name == PaperBag ? RegisterFloor(stands) : StandFloor(stands);
            var target = floor + salesTarget;
            var raw = target - stock;
            int need;
            if (fixtureCap > 0)
            {
                var headroom = fixtureCap - stock;
                if (headroom < MinLineAmount || raw < MinLineAmount)
                {
                    need = MinLineAmount;
                }
                else
                {
                    need = raw < headroom ? raw : headroom;
                }
            }
            else
            {
                need = raw < MinLineAmount ? MinLineAmount : raw;
            }

            return new CoverPlan(
                name,
                guess,
                samples,
                older,
                newer,
                average,
                gap,
                window,
                replace,
                next,
                need,
                stock,
                target,
                fixtureCap);
        }

        private static int CeilUnits(decimal value)
        {
            if (value <= 0)
            {
                return 0;
            }

            return (int)Math.Ceiling(value);
        }

        public static string CoverLine(CoverPlan? plan, int day, string? street, int number)
        {
            if (plan == null)
            {
                return "";
            }

            var sold = plan.Guess
                ? ""
                : plan.Samples == 1
                    ? plan.NewerSold.ToString(CultureInfo.InvariantCulture)
                    : plan.OlderSold.ToString(CultureInfo.InvariantCulture)
                        + "|"
                        + plan.NewerSold.ToString(CultureInfo.InvariantCulture);
            return "contract.cover version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + (street ?? "")
                + " number=" + number.ToString(CultureInfo.InvariantCulture)
                + " item=" + plan.Item
                + " sold=" + sold
                + " average=" + plan.Average.ToString("0.##", CultureInfo.InvariantCulture)
                + " gap=" + plan.Gap.ToString(CultureInfo.InvariantCulture)
                + " window=" + plan.Window.ToString(CultureInfo.InvariantCulture)
                + " multiplier=" + SalesWindowMultiplier.ToString("0.0", CultureInfo.InvariantCulture)
                + " onHand=" + plan.OnHand.ToString(CultureInfo.InvariantCulture)
                + " target=" + plan.Target.ToString(CultureInfo.InvariantCulture)
                + " need=" + plan.Need.ToString(CultureInfo.InvariantCulture)
                + " replace=" + (plan.Replace ? "true" : "false")
                + " next=" + plan.NextDay.ToString(CultureInfo.InvariantCulture)
                + " guess=" + (plan.Guess ? "true" : "false");
        }

        public static string RecordCover(string localAppData, CoverPlan? plan, int day, string? street, int number)
        {
            var line = CoverLine(plan, day, street, number);
            if (line.Length == 0 || string.IsNullOrEmpty(localAppData))
            {
                return line;
            }

            var path = LoadLog.CeoLogPath(localAppData);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + Environment.NewLine);
            return line;
        }

        public static IReadOnlyList<WholesalerOffer> StageOne()
        {
            return new[]
            {
                new WholesalerOffer("ba:businesstype_giftshop", "ba:street_firststreet 37"),
            };
        }

        /// <summary>
        /// Sellable stock tokens from StreamingAssets GiftShop/*/GiftShopWholesalers.json.
        /// </summary>
        public static IReadOnlyList<string> GiftShopWholesalersStock()
        {
            return new[]
            {
                "ba:itemname_cheapgift",
                "ba:itemname_cheapflower",
                "ba:itemname_sodacan",
                "ba:itemname_paperbag",
                "ba:itemname_umbrella",
            };
        }

        public static IReadOnlyList<string> Catalog(string? businessType)
        {
            if (businessType == "ba:businesstype_giftshop")
            {
                return GiftShopWholesalersStock();
            }

            if (businessType == "ba:businesstype_florist")
            {
                return new[]
                {
                    "ba:itemname_cheapflower",
                    "ba:itemname_expensiveflower",
                };
            }

            return Array.Empty<string>();
        }

        public static bool Covers(IReadOnlyList<string>? needed, IReadOnlyList<string>? catalog)
        {
            if (needed == null || needed.Count == 0 || catalog == null || catalog.Count == 0)
            {
                return false;
            }

            foreach (var item in needed)
            {
                if (string.IsNullOrEmpty(item))
                {
                    continue;
                }

                var found = false;
                foreach (var entry in catalog)
                {
                    if (entry == item)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    return false;
                }
            }

            return true;
        }

        public static WholesalerPick Sign(
            BuildingRent? rent,
            StarterPick? layout,
            IReadOnlyList<WholesalerOffer>? offers,
            bool alreadySigned,
            ApiNote? api)
        {
            var call = api == null ? "" : api.Call("wholesaler contract");
            if (alreadySigned
                || rent == null
                || !rent.Rented
                || rent.Street.Length == 0
                || rent.Number <= 0
                || layout == null
                || !layout.Loaded
                || offers == null
                || call.Length == 0)
            {
                return None();
            }

            WholesalerOffer? winner = null;
            foreach (var offer in offers)
            {
                if (offer == null
                    || offer.BusinessType.Length == 0
                    || offer.Wholesale.Length == 0
                    || offer.BusinessType != layout.BusinessType)
                {
                    continue;
                }

                winner = offer;
            }

            if (winner == null)
            {
                return None();
            }

            return new WholesalerPick(winner.Wholesale, rent.Street, rent.Number, true, call);
        }

        public static string Record(string localAppData, WholesalerPick? pick, int day)
        {
            if (pick == null || !pick.Signed)
            {
                return "";
            }

            var line = "contract.signed version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + pick.Street
                + " number=" + pick.Number.ToString(CultureInfo.InvariantCulture)
                + " wholesale=" + pick.Wholesale
                + " call=" + pick.Call;
            var path = LoadLog.CeoLogPath(localAppData ?? "");
            if (File.Exists(path))
            {
                foreach (var existing in File.ReadAllLines(path))
                {
                    if (existing == line)
                    {
                        return line;
                    }
                }
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + System.Environment.NewLine);
            return line;
        }

        public static string Miss(string localAppData, WholesalerPick pick, int day)
        {
            var line = "contract.missed version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " street=" + pick.Street
                + " number=" + pick.Number.ToString(CultureInfo.InvariantCulture)
                + " wholesale=" + pick.Wholesale
                + " call=" + pick.Call;
            var path = LoadLog.CeoLogPath(localAppData ?? "");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + System.Environment.NewLine);
            return line;
        }

        private static WholesalerPick None()
        {
            return new WholesalerPick("", "", 0, false, "");
        }
    }

    public sealed class CoverSale
    {
        public CoverSale(int day, int sold, bool open, int storeOrders = 0)
        {
            Day = day;
            Sold = sold;
            Open = open;
            StoreOrders = storeOrders;
        }

        public int Day { get; }

        public int Sold { get; }

        public bool Open { get; }

        public int StoreOrders { get; }
    }

    public sealed class CoverPlan
    {
        public CoverPlan(
            string item,
            bool guess,
            int samples,
            int olderSold,
            int newerSold,
            decimal average,
            int gap,
            int window,
            bool replace,
            int nextDay,
            int need,
            int onHand = 0,
            int target = 0,
            int fixtureCap = 0)
        {
            Item = item ?? "";
            Guess = guess;
            Samples = samples;
            OlderSold = olderSold;
            NewerSold = newerSold;
            Average = average;
            Gap = gap;
            Window = window;
            Replace = replace;
            NextDay = nextDay;
            Need = need;
            OnHand = onHand;
            Target = target;
            FixtureCap = fixtureCap;
        }

        public string Item { get; }

        public bool Guess { get; }

        public int Samples { get; }

        public int OlderSold { get; }

        public int NewerSold { get; }

        public decimal Average { get; }

        public int Gap { get; }

        public int Window { get; }

        public bool Replace { get; }

        public int NextDay { get; }

        public int Need { get; }

        public int OnHand { get; }

        public int Target { get; }

        public int FixtureCap { get; }
    }

    public sealed class WholesalerWatch
    {
        private object? _seen;

        public bool Due(object? save)
        {
            return save != null && !ReferenceEquals(_seen, save);
        }

        public void Mark(object? save)
        {
            _seen = save;
        }
    }
}
