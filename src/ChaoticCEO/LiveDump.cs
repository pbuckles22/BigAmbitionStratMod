using System.Collections.Generic;
using Entities;

namespace ChaoticCEO
{
    public static class LiveDump
    {
        public static string Dump(string localAppData)
        {
            var current = SaveGameManager.Current;
            if (current == null || current.financialSummaries == null)
            {
                return StatementDump.Write(localAppData, current == null ? 0 : current.Day, new FinancialDay[0]);
            }

            var days = new List<FinancialDay>();
            foreach (var summary in current.financialSummaries)
            {
                days.Add(ToDay(summary));
            }

            return StatementDump.Write(localAppData, current.Day, days);
        }

        private static FinancialDay ToDay(FinancialSummary summary)
        {
            if (summary == null)
            {
                return new FinancialDay(0, new StatementRow[0]);
            }

            var rows = new List<StatementRow>();
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
                    rows.Add(new StatementRow(
                        street,
                        number,
                        business.TotalSales,
                        business.TotalResources,
                        business.SalaryExpenses,
                        business.RentExpenses,
                        business.MarketingExpenses,
                        business.Theft,
                        business.TotalProfit));
                }
            }

            return new FinancialDay(summary.dayNumber, rows);
        }
    }
}
