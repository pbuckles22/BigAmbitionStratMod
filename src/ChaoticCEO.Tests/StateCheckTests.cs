using ChaoticCEO;

namespace ChaoticCEO.Tests;

[Collection(nameof(AutopilotState))]
public class StateCheckTests
{
    [Fact]
    public void Null_business_type_after_layout_logs_SILENT_NOOP()
    {
        var line = StateCheck.Line(
            1,
            "layout",
            "ba:street_seventhavenue",
            15,
            "ba:businesstype_giftshop",
            null);
        Assert.Equal(
            "SILENT_NOOP version=" + LoadLog.Version
                + " day=1 where=layout expected=ba:businesstype_giftshop actual= street=ba:street_seventhavenue number=15",
            line);
    }

    [Fact]
    public void Blank_business_type_after_layout_logs_SILENT_NOOP()
    {
        var line = StateCheck.Line(
            1,
            "layout",
            "ba:street_seventhavenue",
            15,
            "ba:businesstype_giftshop",
            "");
        Assert.Equal(
            "SILENT_NOOP version=" + LoadLog.Version
                + " day=1 where=layout expected=ba:businesstype_giftshop actual= street=ba:street_seventhavenue number=15",
            line);
        Assert.DoesNotContain("layout.loaded", line);
    }

    [Fact]
    public void Matching_type_stays_quiet()
    {
        Assert.Equal(
            "",
            StateCheck.Line(
                1,
                "layout",
                "ba:street_seventhavenue",
                15,
                "ba:businesstype_giftshop",
                "ba:businesstype_giftshop"));
    }

    [Fact]
    public void Empty_type_token_is_still_a_noop()
    {
        var line = StateCheck.Line(
            4,
            "layout",
            "ba:street_ninthavenue",
            19,
            "ba:businesstype_giftshop",
            "ba:businesstype_empty");
        Assert.Contains("SILENT_NOOP version=" + LoadLog.Version, line);
        Assert.Contains("expected=ba:businesstype_giftshop", line);
        Assert.Contains("actual=ba:businesstype_empty", line);
        Assert.Contains("number=19", line);
        Assert.DoesNotContain("layout.loaded", line);
    }

    [Fact]
    public void Wrong_type_logs_SILENT_NOOP()
    {
        var line = StateCheck.Line(
            2,
            "layout",
            "ba:street_seventhavenue",
            22,
            "ba:businesstype_giftshop",
            "ba:businesstype_florist");
        Assert.Contains("expected=ba:businesstype_giftshop", line);
        Assert.Contains("actual=ba:businesstype_florist", line);
        Assert.DoesNotContain("layout.loaded", line);
    }

    [Fact]
    public void Rent_that_does_not_stick_logs_SILENT_NOOP()
    {
        var line = StateCheck.Line(3, "rent", "ba:street_seventhavenue", 26, "rented", "not-rented");
        Assert.Equal(
            "SILENT_NOOP version=" + LoadLog.Version
                + " day=3 where=rent expected=rented actual=not-rented street=ba:street_seventhavenue number=26",
            line);
        Assert.DoesNotContain("building.rented", line);
    }

    [Fact]
    public void Rent_that_sticks_stays_quiet()
    {
        Assert.Equal(
            "",
            StateCheck.Line(3, "rent", "ba:street_seventhavenue", 26, "rented", "rented"));
    }

    [Fact]
    public void Blank_expected_stays_quiet()
    {
        Assert.Equal("", StateCheck.Line(1, "layout", "ba:street_a", 1, "", ""));
        Assert.Equal("", StateCheck.Line(1, "layout", "ba:street_a", 1, "ba:businesstype_empty", ""));
        Assert.Equal("", StateCheck.Line(1, "layout", "ba:street_a", 1, "  ", null));
    }

    [Fact]
    public void Record_writes_the_noop_once_and_a_match_writes_nothing()
    {
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-state-check-" + Guid.NewGuid().ToString("N")));
        try
        {
            CrashTrace.Reset();
            var first = StateCheck.Record(
                dir.FullName,
                1,
                "layout",
                "ba:street_seventhavenue",
                15,
                "ba:businesstype_giftshop",
                "");
            var again = StateCheck.Record(
                dir.FullName,
                1,
                "layout",
                "ba:street_seventhavenue",
                15,
                "ba:businesstype_giftshop",
                "");
            var match = StateCheck.Record(
                dir.FullName,
                1,
                "layout",
                "ba:street_seventhavenue",
                15,
                "ba:businesstype_giftshop",
                "ba:businesstype_giftshop");
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));
            var hits = 0;
            foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line == first)
                {
                    hits++;
                }
            }

            Assert.StartsWith("SILENT_NOOP ", first);
            Assert.Equal(first, again);
            Assert.Equal("", match);
            Assert.Equal(1, hits);
            Assert.DoesNotContain("layout.loaded", text);
            Assert.Contains("state.layout", CrashTrace.Trail());
        }
        finally
        {
            CrashTrace.Reset();
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Chain_named_block_then_blank_layout_is_SILENT_NOOP()
    {
        // Chain: named block → state check
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-state-check-chain-" + Guid.NewGuid().ToString("N")));
        try
        {
            CrashTrace.Reset();
            var blocked = NamedBlock.Record(
                dir.FullName,
                1,
                "layout",
                "not-inside",
                "ba:street_seventhavenue",
                15);
            var verdict = StateCheck.Record(
                dir.FullName,
                int.Parse(Field(blocked, "day")),
                Field(blocked, "where"),
                Field(blocked, "street"),
                int.Parse(Field(blocked, "number")),
                "ba:businesstype_giftshop",
                "");
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));

            Assert.Contains("slug=not-inside", blocked);
            Assert.StartsWith("SILENT_NOOP version=" + LoadLog.Version, verdict);
            Assert.Contains("expected=ba:businesstype_giftshop", verdict);
            Assert.Contains("actual= street=" + Field(blocked, "street"), verdict);
            Assert.Contains("number=" + Field(blocked, "number"), verdict);
            Assert.Contains(blocked, text);
            Assert.Contains(verdict, text);
            Assert.DoesNotContain("layout.loaded", text);
        }
        finally
        {
            CrashTrace.Reset();
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Chain_named_block_then_matching_layout_keeps_loaded()
    {
        // Chain: named block → state check
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-state-check-match-" + Guid.NewGuid().ToString("N")));
        try
        {
            var blocked = NamedBlock.Line(8, "layout", "not-inside", "ba:street_seventhavenue", 20);
            var day = int.Parse(Field(blocked, "day"));
            var street = Field(blocked, "street");
            var number = int.Parse(Field(blocked, "number"));
            var verdict = StateCheck.Line(
                day,
                Field(blocked, "where"),
                street,
                number,
                "ba:businesstype_giftshop",
                "ba:businesstype_giftshop");
            var pick = new StarterPick(
                "ba:businesstype_giftshop",
                "GiftShopWholesalers",
                "Starter Gift",
                true,
                GameApi.Live().Call("clone"),
                8,
                16);
            var rent = new BuildingRent(street, number, true, GameApi.Live().Call("clone"));
            var loaded = verdict.Length == 0
                ? Starter.Record(dir.FullName, pick, rent, day)
                : verdict;
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));

            Assert.Equal("", verdict);
            Assert.StartsWith("layout.loaded version=" + LoadLog.Version, loaded);
            Assert.Contains("type=ba:businesstype_giftshop", loaded);
            Assert.Contains("street=" + street, loaded);
            Assert.Contains("number=" + number.ToString(), loaded);
            Assert.DoesNotContain("SILENT_NOOP", text);
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Chain_named_block_then_rent_noop_skips_building_rented()
    {
        // Chain: named block → state check
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-state-check-rent-" + Guid.NewGuid().ToString("N")));
        try
        {
            var blocked = NamedBlock.Line(3, "layout", "no-lot", "ba:street_seventhavenue", 26);
            var day = int.Parse(Field(blocked, "day"));
            var street = Field(blocked, "street");
            var number = int.Parse(Field(blocked, "number"));
            var verdict = StateCheck.Line(day, "rent", street, number, "rented", "not-rented");
            var written = verdict.Length == 0
                ? Rent.Record(dir.FullName, new BuildingRent(street, number, true, GameApi.Live().Call("rent")), day)
                : StateCheck.Record(dir.FullName, day, "rent", street, number, "rented", "not-rented");
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));

            Assert.StartsWith("SILENT_NOOP version=" + LoadLog.Version, written);
            Assert.Contains("where=rent", written);
            Assert.Contains("expected=rented", written);
            Assert.Contains("actual=not-rented", written);
            Assert.Contains("street=" + street, written);
            Assert.Contains("number=" + number.ToString(), written);
            Assert.DoesNotContain("building.rented", text);
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Chain_named_block_then_rent_match_keeps_building_rented()
    {
        // Chain: named block → state check
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-state-check-rent-match-" + Guid.NewGuid().ToString("N")));
        try
        {
            var blocked = NamedBlock.Line(3, "layout", "no-lot", "ba:street_seventhavenue", 26);
            var day = int.Parse(Field(blocked, "day"));
            var street = Field(blocked, "street");
            var number = int.Parse(Field(blocked, "number"));
            var verdict = StateCheck.Line(day, "rent", street, number, "rented", "rented");
            var written = verdict.Length == 0
                ? Rent.Record(
                    dir.FullName,
                    new BuildingRent(street, number, true, GameApi.Live().Call("rent")),
                    day)
                : verdict;
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));

            Assert.Equal("", verdict);
            Assert.StartsWith("building.rented version=" + LoadLog.Version, written);
            Assert.Contains("street=" + street, written);
            Assert.Contains("number=" + number.ToString(), written);
            Assert.Contains("call=Buildings.BuildingCommands.Command_RentBuilding", written);
            Assert.DoesNotContain("SILENT_NOOP", text);
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    private static string Field(string line, string name)
    {
        var token = name + "=";
        var start = line.IndexOf(token, StringComparison.Ordinal);
        Assert.True(start >= 0, name);
        start += token.Length;
        var end = line.IndexOf(' ', start);
        if (end < 0)
        {
            end = line.Length;
        }

        return line.Substring(start, end - start);
    }
}
