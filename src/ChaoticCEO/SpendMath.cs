using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ChaoticCEO
{
    public sealed class SpendStore
    {
        public SpendStore(
            string street,
            int number,
            string kind,
            decimal sales,
            decimal goods,
            decimal wages,
            decimal rent,
            decimal marketing,
            decimal theft,
            decimal license,
            decimal profit)
        {
            Street = street ?? "";
            Number = number;
            Kind = kind ?? "";
            Sales = sales;
            Goods = goods;
            Wages = wages;
            Rent = rent;
            Marketing = marketing;
            Theft = theft;
            License = license;
            Profit = profit;
        }

        public string Street { get; }

        public int Number { get; }

        public string Kind { get; }

        public decimal Sales { get; }

        public decimal Goods { get; }

        public decimal Wages { get; }

        public decimal Rent { get; }

        public decimal Marketing { get; }

        public decimal Theft { get; }

        public decimal License { get; }

        public decimal Profit { get; }
    }

    public sealed class SpendItem
    {
        public SpendItem(string street, int number, string kind, string side, string name, decimal amount)
        {
            Street = street ?? "";
            Number = number;
            Kind = kind ?? "";
            Side = side ?? "";
            Name = name ?? "";
            Amount = amount;
        }

        public string Street { get; }

        public int Number { get; }

        public string Kind { get; }

        public string Side { get; }

        public string Name { get; }

        public decimal Amount { get; }
    }

    public sealed class SpendPlace
    {
        public SpendPlace(string street, int number, string kind, decimal amount)
        {
            Street = street ?? "";
            Number = number;
            Kind = kind ?? "";
            Amount = amount;
        }

        public string Street { get; }

        public int Number { get; }

        public string Kind { get; }

        public decimal Amount { get; }
    }

    public sealed class SpendMove
    {
        public SpendMove(string type, string cats, string street, int number, string kind, decimal amount)
        {
            Type = type ?? "";
            Cats = cats ?? "";
            Street = street ?? "";
            Number = number;
            Kind = kind ?? "";
            Amount = amount;
        }

        public string Type { get; }

        public string Cats { get; }

        public string Street { get; }

        public int Number { get; }

        public string Kind { get; }

        public decimal Amount { get; }
    }

    public sealed class SpendEmpire
    {
        public SpendEmpire(
            decimal business,
            decimal realEstate,
            decimal residential,
            decimal loan,
            decimal parking,
            decimal unassigned,
            decimal salary,
            decimal insurance,
            decimal headhunter,
            decimal interest,
            decimal total)
        {
            Business = business;
            RealEstate = realEstate;
            Residential = residential;
            Loan = loan;
            Parking = parking;
            Unassigned = unassigned;
            Salary = salary;
            Insurance = insurance;
            Headhunter = headhunter;
            Interest = interest;
            Total = total;
        }

        public decimal Business { get; }

        public decimal RealEstate { get; }

        public decimal Residential { get; }

        public decimal Loan { get; }

        public decimal Parking { get; }

        public decimal Unassigned { get; }

        public decimal Salary { get; }

        public decimal Insurance { get; }

        public decimal Headhunter { get; }

        public decimal Interest { get; }

        public decimal Total { get; }
    }

    public static class SpendKind
    {
        public static string Of(string? type)
        {
            var name = type ?? "";
            if (name.Length == 0)
            {
                return "other";
            }

            if (name == "retail" || name == "goods" || name == "food" || name == "durables" || name == "specialty")
            {
                return "retail";
            }

            if (name == "service")
            {
                return "service";
            }

            if (name == "cost"
                || name == "warehouse"
                || name == "office"
                || name == "headquarters"
                || name == "hq"
                || name == "ba:businesstype_headquarters"
                || name == "ba:businesstype_warehouse")
            {
                return "cost";
            }

            var family = StoreFamily.For(name);
            if (family == "food" || family == "durables" || family == "specialty")
            {
                return "retail";
            }

            if (family == "service")
            {
                return "service";
            }

            if (family == "warehouse" || family == "office")
            {
                return "cost";
            }

            return "other";
        }
    }

    public static class SpendCash
    {
        private static decimal? _close;

        public static void Reset()
        {
            _close = null;
        }

        public static decimal? Take(decimal close)
        {
            var open = _close;
            _close = close;
            return open;
        }
    }

    public static class SpendMath
    {
        public static string Blocked(int day, string reason)
        {
            return "spend.blocked version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " reason=" + Token(reason);
        }

        public static IReadOnlyList<string> Lines(
            int day,
            IReadOnlyList<SpendStore>? stores,
            IReadOnlyList<SpendItem>? items,
            IReadOnlyList<SpendPlace>? homes,
            IReadOnlyList<SpendPlace>? properties,
            SpendEmpire? empire,
            IReadOnlyList<SpendMove>? moves,
            decimal? cashOpen,
            decimal cashClose)
        {
            var books = CopyStores(stores);
            var named = CopyItems(items);
            var houses = CopyPlaces(homes);
            var lots = CopyPlaces(properties);
            var wallet = CopyMoves(moves);
            var totals = empire ?? new SpendEmpire(0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m);
            var lines = new List<string>();
            decimal retail = 0m;
            decimal service = 0m;
            decimal cost = 0m;
            decimal other = 0m;
            foreach (var store in books)
            {
                var sales = Money(store.Sales);
                var goods = Money(store.Goods);
                var wages = Money(store.Wages);
                var rent = Money(store.Rent);
                var marketing = Money(store.Marketing);
                var theft = Money(store.Theft);
                var license = Money(store.License);
                var profit = Money(store.Profit);
                var ongoing = wages + rent + marketing + theft + license;
                var check = sales - goods - ongoing;
                var gap = profit - check;
                AddKind(store.Kind, profit, ref retail, ref service, ref cost, ref other);
                lines.Add(
                    "spend.store version=" + LoadLog.Version
                    + " day=" + Day(day)
                    + " street=" + Token(store.Street)
                    + " number=" + store.Number.ToString(CultureInfo.InvariantCulture)
                    + " kind=" + Token(store.Kind)
                    + " sales=" + Format(sales)
                    + " goods=" + Format(goods)
                    + " wages=" + Format(wages)
                    + " rent=" + Format(rent)
                    + " marketing=" + Format(marketing)
                    + " theft=" + Format(theft)
                    + " license=" + Format(license)
                    + " ongoing=" + Format(ongoing)
                    + " profit=" + Format(profit)
                    + " check=" + Format(check)
                    + " gap=" + Format(gap));
            }

            foreach (var item in named)
            {
                if (Money(item.Amount) == 0m)
                {
                    continue;
                }

                lines.Add(
                    "spend.item version=" + LoadLog.Version
                    + " day=" + Day(day)
                    + " street=" + Token(item.Street)
                    + " number=" + item.Number.ToString(CultureInfo.InvariantCulture)
                    + " kind=" + Token(item.Kind)
                    + " side=" + Token(item.Side)
                    + " name=" + Token(item.Name)
                    + " amount=" + Format(Money(item.Amount)));
            }

            var storeSum = retail + service + cost + other;
            lines.Add(
                "spend.roll version=" + LoadLog.Version
                + " day=" + Day(day)
                + " retail=" + Format(retail)
                + " service=" + Format(service)
                + " cost=" + Format(cost)
                + " other=" + Format(other)
                + " stores=" + Format(storeSum));
            foreach (var home in houses)
            {
                if (Money(home.Amount) == 0m)
                {
                    continue;
                }

                lines.Add(
                    "spend.home version=" + LoadLog.Version
                    + " day=" + Day(day)
                    + " street=" + Token(home.Street)
                    + " number=" + home.Number.ToString(CultureInfo.InvariantCulture)
                    + " amount=" + Format(Money(home.Amount)));
            }

            foreach (var lot in lots)
            {
                if (Money(lot.Amount) == 0m)
                {
                    continue;
                }

                lines.Add(
                    "spend.property version=" + LoadLog.Version
                    + " day=" + Day(day)
                    + " street=" + Token(lot.Street)
                    + " number=" + lot.Number.ToString(CultureInfo.InvariantCulture)
                    + " kind=" + Token(lot.Kind)
                    + " amount=" + Format(Money(lot.Amount)));
            }

            var business = Money(totals.Business);
            var realEstate = Money(totals.RealEstate);
            var residential = Money(totals.Residential);
            var loan = Money(totals.Loan);
            var parking = Money(totals.Parking);
            var unassigned = Money(totals.Unassigned);
            var salary = Money(totals.Salary);
            var insurance = Money(totals.Insurance);
            var headhunter = Money(totals.Headhunter);
            var interest = Money(totals.Interest);
            var total = Money(totals.Total);
            var empireCheck = business + realEstate - residential + loan + parking + unassigned + salary + insurance + headhunter;
            var empireGap = total - empireCheck;
            var businessGap = business - storeSum;
            lines.Add(
                "spend.empire version=" + LoadLog.Version
                + " day=" + Day(day)
                + " business=" + Format(business)
                + " realEstate=" + Format(realEstate)
                + " residential=" + Format(residential)
                + " loan=" + Format(loan)
                + " parking=" + Format(parking)
                + " unassigned=" + Format(unassigned)
                + " salary=" + Format(salary)
                + " insurance=" + Format(insurance)
                + " headhunter=" + Format(headhunter)
                + " interest=" + Format(interest)
                + " total=" + Format(total)
                + " check=" + Format(empireCheck)
                + " gap=" + Format(empireGap)
                + " businessGap=" + Format(businessGap));
            decimal namedMoves = 0m;
            foreach (var move in wallet)
            {
                var amount = Money(move.Amount);
                if (amount == 0m)
                {
                    continue;
                }

                namedMoves += amount;
                lines.Add(
                    "spend.move version=" + LoadLog.Version
                    + " day=" + Day(day)
                    + " type=" + Token(move.Type)
                    + " cats=" + Token(move.Cats)
                    + " street=" + Token(move.Street)
                    + " number=" + move.Number.ToString(CultureInfo.InvariantCulture)
                    + " kind=" + Token(move.Kind)
                    + " amount=" + Format(amount));
            }

            var close = Money(cashClose);
            if (cashOpen.HasValue)
            {
                var open = Money(cashOpen.Value);
                var delta = close - open;
                lines.Add(
                    "spend.cash version=" + LoadLog.Version
                    + " day=" + Day(day)
                    + " open=" + Format(open)
                    + " close=" + Format(close)
                    + " delta=" + Format(delta)
                    + " moves=" + Format(namedMoves)
                    + " gap=" + Format(delta - namedMoves));
            }
            else
            {
                lines.Add(
                    "spend.cash version=" + LoadLog.Version
                    + " day=" + Day(day)
                    + " open=missing"
                    + " close=" + Format(close)
                    + " delta=missing"
                    + " moves=" + Format(namedMoves)
                    + " gap=missing");
            }

            return lines;
        }

        private static void AddKind(
            string kind,
            decimal profit,
            ref decimal retail,
            ref decimal service,
            ref decimal cost,
            ref decimal other)
        {
            if (kind == "retail")
            {
                retail += profit;
                return;
            }

            if (kind == "service")
            {
                service += profit;
                return;
            }

            if (kind == "cost")
            {
                cost += profit;
                return;
            }

            other += profit;
        }

        private static List<SpendStore> CopyStores(IReadOnlyList<SpendStore>? stores)
        {
            var copy = new List<SpendStore>();
            if (stores == null)
            {
                return copy;
            }

            foreach (var store in stores)
            {
                if (store != null)
                {
                    copy.Add(store);
                }
            }

            copy.Sort(CompareStore);
            return copy;
        }

        private static List<SpendItem> CopyItems(IReadOnlyList<SpendItem>? items)
        {
            var copy = new List<SpendItem>();
            if (items == null)
            {
                return copy;
            }

            foreach (var item in items)
            {
                if (item != null)
                {
                    copy.Add(item);
                }
            }

            copy.Sort(CompareItem);
            return copy;
        }

        private static List<SpendPlace> CopyPlaces(IReadOnlyList<SpendPlace>? places)
        {
            var copy = new List<SpendPlace>();
            if (places == null)
            {
                return copy;
            }

            foreach (var place in places)
            {
                if (place != null)
                {
                    copy.Add(place);
                }
            }

            copy.Sort(ComparePlace);
            return copy;
        }

        private static List<SpendMove> CopyMoves(IReadOnlyList<SpendMove>? moves)
        {
            var copy = new List<SpendMove>();
            if (moves == null)
            {
                return copy;
            }

            foreach (var move in moves)
            {
                if (move != null)
                {
                    copy.Add(move);
                }
            }

            copy.Sort(CompareMove);
            return copy;
        }

        private static int CompareStore(SpendStore left, SpendStore right)
        {
            var rank = Rank(left.Kind).CompareTo(Rank(right.Kind));
            if (rank != 0)
            {
                return rank;
            }

            var street = string.Compare(Token(left.Street), Token(right.Street), StringComparison.Ordinal);
            if (street != 0)
            {
                return street;
            }

            return left.Number.CompareTo(right.Number);
        }

        private static int CompareItem(SpendItem left, SpendItem right)
        {
            var rank = Rank(left.Kind).CompareTo(Rank(right.Kind));
            if (rank != 0)
            {
                return rank;
            }

            var street = string.Compare(Token(left.Street), Token(right.Street), StringComparison.Ordinal);
            if (street != 0)
            {
                return street;
            }

            var number = left.Number.CompareTo(right.Number);
            if (number != 0)
            {
                return number;
            }

            var side = SideRank(left.Side).CompareTo(SideRank(right.Side));
            if (side != 0)
            {
                return side;
            }

            return string.Compare(Token(left.Name), Token(right.Name), StringComparison.Ordinal);
        }

        private static int ComparePlace(SpendPlace left, SpendPlace right)
        {
            var street = string.Compare(Token(left.Street), Token(right.Street), StringComparison.Ordinal);
            if (street != 0)
            {
                return street;
            }

            return left.Number.CompareTo(right.Number);
        }

        private static int CompareMove(SpendMove left, SpendMove right)
        {
            var type = string.Compare(Token(left.Type), Token(right.Type), StringComparison.Ordinal);
            if (type != 0)
            {
                return type;
            }

            var street = string.Compare(Token(left.Street), Token(right.Street), StringComparison.Ordinal);
            if (street != 0)
            {
                return street;
            }

            var number = left.Number.CompareTo(right.Number);
            if (number != 0)
            {
                return number;
            }

            return Money(left.Amount).CompareTo(Money(right.Amount));
        }

        private static int Rank(string kind)
        {
            if (kind == "retail")
            {
                return 0;
            }

            if (kind == "service")
            {
                return 1;
            }

            if (kind == "cost")
            {
                return 2;
            }

            return 3;
        }

        private static int SideRank(string side)
        {
            if (side == "sales")
            {
                return 0;
            }

            if (side == "goods")
            {
                return 1;
            }

            return 2;
        }

        private static string Day(int day)
        {
            return day.ToString(CultureInfo.InvariantCulture);
        }

        private static decimal Money(decimal value)
        {
            return decimal.Round(value, 4, MidpointRounding.ToEven);
        }

        private static string Format(decimal value)
        {
            var rounded = Money(value);
            if (rounded == decimal.Truncate(rounded))
            {
                return decimal.Truncate(rounded).ToString(CultureInfo.InvariantCulture);
            }

            return rounded.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static string Token(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            var text = new StringBuilder(value.Length);
            var spaced = false;
            foreach (var letter in value)
            {
                if (letter == ' ' || letter == '\t' || letter == '\r' || letter == '\n' || letter == '=')
                {
                    if (!spaced && text.Length > 0)
                    {
                        text.Append('_');
                    }

                    spaced = true;
                    continue;
                }

                spaced = false;
                text.Append(letter);
            }

            return text.ToString();
        }
    }
}
