using System;
using System.Collections.Generic;
using System.Globalization;
using Entities;

namespace ChaoticCEO
{
    public static class SpendDump
    {
        public static void Reset()
        {
            SpendCash.Reset();
        }

        public static IReadOnlyList<string> Lines(GameInstance? current)
        {
            if (current == null)
            {
                return new[] { SpendMath.Blocked(0, "no_save") };
            }

            var closed = current.Day - 1;
            var close = Cash(current);
            var open = SpendCash.Take(close);
            var summary = Yesterday(current, closed);
            if (summary == null)
            {
                return new[] { SpendMath.Blocked(closed, "no_summary") };
            }

            var kinds = Kinds(current);
            var stores = new List<SpendStore>();
            var items = new List<SpendItem>();
            if (summary.businessIncomeStatements != null)
            {
                foreach (var business in summary.businessIncomeStatements)
                {
                    if (business == null)
                    {
                        continue;
                    }

                    var address = business.Address;
                    var street = address == null || address.streetName == null ? "" : address.streetName;
                    var number = address == null ? 0 : address.streetNumber;
                    var kind = KindAt(kinds, street, number);
                    stores.Add(new SpendStore(
                        street,
                        number,
                        kind,
                        (decimal)business.TotalSales,
                        (decimal)business.TotalResources,
                        (decimal)business.SalaryExpenses,
                        (decimal)business.RentExpenses,
                        (decimal)business.MarketingExpenses,
                        (decimal)business.Theft,
                        (decimal)business.LicensingFees,
                        (decimal)business.TotalProfit));
                    AddItems(items, business.Sales, street, number, kind, "sales");
                    AddItems(items, business.Resources, street, number, kind, "goods");
                }
            }

            var homes = new List<SpendPlace>();
            if (summary.residentialStatements != null)
            {
                foreach (var home in summary.residentialStatements)
                {
                    if (home == null)
                    {
                        continue;
                    }

                    var address = home.Address;
                    homes.Add(new SpendPlace(
                        address == null || address.streetName == null ? "" : address.streetName,
                        address == null ? 0 : address.streetNumber,
                        "home",
                        (decimal)home.Amount));
                }
            }

            var properties = new List<SpendPlace>();
            if (summary.realEstateStatements != null)
            {
                foreach (var lot in summary.realEstateStatements)
                {
                    if (lot == null)
                    {
                        continue;
                    }

                    var address = lot.Address;
                    var street = address == null || address.streetName == null ? "" : address.streetName;
                    var number = address == null ? 0 : address.streetNumber;
                    properties.Add(new SpendPlace(street, number, KindAt(kinds, street, number), (decimal)lot.Amount));
                }
            }

            var moves = new List<SpendMove>();
            if (current.Transactions != null)
            {
                foreach (var move in current.Transactions)
                {
                    if (move == null || move.timestamp == null || move.timestamp.Day != closed)
                    {
                        continue;
                    }

                    var address = move.address;
                    var street = address == null || address.streetName == null ? "" : address.streetName;
                    var number = address == null ? 0 : address.streetNumber;
                    var kind = address == null ? "player" : KindAt(kinds, street, number);
                    moves.Add(new SpendMove(
                        move.transactionType,
                        Cats(move),
                        street,
                        number,
                        kind,
                        (decimal)move.amount));
                }
            }

            return SpendMath.Lines(
                closed,
                stores,
                items,
                homes,
                properties,
                new SpendEmpire(
                    (decimal)summary.totalBusinessProfit,
                    (decimal)summary.totalRealEstate,
                    (decimal)summary.totalResidentialExpenses,
                    (decimal)summary.totalLoanExpenses,
                    (decimal)summary.parkingFees,
                    (decimal)summary.totalUnassignedStaffWages,
                    (decimal)summary.salaryIncome,
                    (decimal)summary.totalHealthInsuranceExpenses,
                    (decimal)summary.totalHeadhunterReplacementFees,
                    (decimal)summary.negativeInterestRates,
                    (decimal)summary.totalProfit),
                moves,
                open,
                close);
        }

        private static void AddItems(
            List<SpendItem> items,
            List<FinancialSummary.BusinessIncomeStatement.TransactionGroupEntry>? entries,
            string street,
            int number,
            string kind,
            string side)
        {
            if (entries == null)
            {
                return;
            }

            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    continue;
                }

                items.Add(new SpendItem(street, number, kind, side, entry.ItemName, (decimal)entry.Amount));
            }
        }

        private static string Cats(Transaction move)
        {
            if (move.transactionCategories == null || move.transactionCategories.Count == 0)
            {
                return "";
            }

            var names = new List<string>();
            foreach (var name in move.transactionCategories)
            {
                if (!string.IsNullOrEmpty(name))
                {
                    names.Add(name);
                }
            }

            names.Sort(StringComparer.Ordinal);
            return string.Join(",", names);
        }

        private static FinancialSummary? Yesterday(GameInstance current, int closed)
        {
            if (current.financialSummaries == null)
            {
                return null;
            }

            FinancialSummary? found = null;
            foreach (var summary in current.financialSummaries)
            {
                if (summary != null && summary.dayNumber == closed)
                {
                    found = summary;
                }
            }

            return found;
        }

        private static Dictionary<string, string> Kinds(GameInstance current)
        {
            var kinds = new Dictionary<string, string>(StringComparer.Ordinal);
            if (current.BuildingRegistrations == null)
            {
                return kinds;
            }

            foreach (var building in current.BuildingRegistrations)
            {
                if (building == null || string.IsNullOrEmpty(building.StreetName) || building.StreetNumber <= 0)
                {
                    continue;
                }

                var key = building.StreetName + "\n" + building.StreetNumber.ToString(CultureInfo.InvariantCulture);
                var player = building.RentedByPlayer || building.BuildingOwnedByPlayer;
                if (kinds.ContainsKey(key) && !player)
                {
                    continue;
                }

                kinds[key] = building.businessTypeName ?? "";
            }

            return kinds;
        }

        private static string KindAt(Dictionary<string, string> kinds, string street, int number)
        {
            if (street.Length == 0 || number <= 0)
            {
                return "other";
            }

            var key = street + "\n" + number.ToString(CultureInfo.InvariantCulture);
            if (!kinds.TryGetValue(key, out var type))
            {
                return "other";
            }

            return SpendKind.Of(type);
        }

        private static decimal Cash(GameInstance current)
        {
            return Convert.ToDecimal(Helpers.PlayerHelper.GetPersonalWealth().cash, CultureInfo.InvariantCulture);
        }
    }
}
