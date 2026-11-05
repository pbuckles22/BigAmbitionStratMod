using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class StatementDumpTests
{
    [Fact]
    public void CsvWritten_locks_yesterday()
    {
        using var dir = new TempDump();
        var days = new[]
        {
            Day(1014, Row("ba:street_today", 1, 9f, 0f, 0f, 0f, 0f, 0f, 9f)),
            Day(
                1013,
                Row("ba:street_fourthavenue", 28, 65477f, 3897.2825f, 9793.2383f, 1099f, 0f, 0f, 50687.4805f),
                Row("ba:street_firstavenue", 38, 89069f, 12715.6709f, 3102.3333f, 1069f, 0f, 0f, 72181.9922f)),
        };

        var line = StatementDump.Write(dir.Path, 1014, days);

        var csv = File.ReadAllText(StatementDump.CsvPath(dir.Path)).Replace("\r\n", "\n");
        Assert.Equal(
            """
            day,street,number,sales,goods,wages,rent,marketing,theft,profit
            1013,ba:street_fourthavenue,28,65477,3897.2825,9793.2383,1099,0,0,50687.4805
            1013,ba:street_firstavenue,38,89069,12715.6709,3102.3333,1069,0,0,72181.9922
            """.Replace("\r\n", "\n") + "\n",
            csv);
        Assert.DoesNotContain("ba:street_today", csv);
        Assert.Equal(
            "csv.written version=1.11.4 rows=2 day=1013 street=ba:street_fourthavenue number=28 profit=50687.4805",
            line);
        Assert.Equal(line, File.ReadAllText(Path.Combine(dir.Path, "ChaoticCEO", "ceo.log")).TrimEnd('\r', '\n'));
    }

    [Fact]
    public void CsvWritten_skips_today_when_yesterday_is_missing()
    {
        using var dir = new TempDump();

        var line = StatementDump.Write(
            dir.Path,
            1014,
            new[] { new FinancialDay(1014, new[] { new StatementRow("ba:street_today", 1, 9f, 0f, 0f, 0f, 0f, 0f, 9f) }) });

        var csv = File.ReadAllText(StatementDump.CsvPath(dir.Path)).Replace("\r\n", "\n");
        Assert.Equal("day,street,number,sales,goods,wages,rent,marketing,theft,profit\n", csv);
        Assert.Equal("csv.written version=1.11.4 rows=0 day=1013 street= number= profit=", line);
        Assert.DoesNotContain("ba:street_today", csv);
    }

    [Fact]
    public void CsvWritten_keeps_the_first_yesterday()
    {
        using var dir = new TempDump();
        var days = new[]
        {
            new FinancialDay(1013, new[] { new StatementRow("ba:street_fourthavenue", 28, 10f, 0f, 0f, 0f, 0f, 0f, 10f) }),
            new FinancialDay(1013, new[] { new StatementRow("ba:street_later", 2, 99f, 0f, 0f, 0f, 0f, 0f, 99f) }),
        };

        var line = StatementDump.Write(dir.Path, 1014, days);

        Assert.Equal(
            "csv.written version=1.11.4 rows=1 day=1013 street=ba:street_fourthavenue number=28 profit=10",
            line);
        Assert.DoesNotContain("ba:street_later", File.ReadAllText(StatementDump.CsvPath(dir.Path)));
    }

    private static FinancialDay Day(int day, params StatementRow[] rows)
    {
        return new FinancialDay(day, rows);
    }

    private static StatementRow Row(
        string street,
        int number,
        float sales,
        float goods,
        float wages,
        float rent,
        float marketing,
        float theft,
        float profit)
    {
        return new StatementRow(street, number, sales, goods, wages, rent, marketing, theft, profit);
    }

    private sealed class TempDump : IDisposable
    {
        public TempDump()
        {
            Path = Directory.CreateTempSubdirectory("chaotic-csv").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
