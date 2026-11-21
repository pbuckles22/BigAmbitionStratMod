using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class CourseGateTests
{
    [Fact]
    public void Bare_second_lot_names_market_demands()
    {
        // Harvest: day-5 51 3rd Street stayed kind=other rent-only. BizMan showed Market Demands. No fit-out line.
        var lot = new BareLeaseFact(true, "", "ba:street_thirdstreet", 51);
        Assert.True(BareLease.IsBare(lot));
        Assert.Equal("other", SpendKind.Of(lot.BusinessType));

        var priors = new[] { StoreHealth.Healthy("ba:street_seventhavenue 15") };
        var place = lot.Street + " " + lot.Number.ToString();
        var pick = FitOut.Decide(priors, place, FitOutProgress.Bare(), "Market Demands");

        Assert.False(pick.Chosen);
        Assert.Equal("", pick.Step);
        Assert.Equal("market-demands", pick.Gate);
        Assert.Equal(place, pick.Place);
        Assert.Equal(
            "course.needed version=" + LoadLog.Version
                + " day=5 course=market-demands street=ba:street_thirdstreet number=51",
            CourseGate.Line(pick.Gate, 5, lot.Street, lot.Number));
        Assert.Equal("market-demands", CourseGate.Slug("diplomaname_marketdemands"));
        Assert.Equal("market-demands", CourseGate.Slug("Market Demands"));
    }

    [Fact]
    public void Expansion_refusal_names_the_gate()
    {
        var notReady = new[]
        {
            new StoreHealth(true, true, false, true, true, "ba:street_seventhavenue 15"),
        };
        var place = "ba:street_thirdstreet 51";
        var pick = FitOut.Decide(notReady, place, FitOutProgress.Bare());

        Assert.False(pick.Chosen);
        Assert.False(StoreHealthGate.ExpansionAllowed(notReady));
        Assert.Equal(CourseGate.ExpansionSlug, pick.Gate);
        Assert.Equal(
            NamedBlock.Line(5, "fitout", CourseGate.ExpansionSlug, "ba:street_thirdstreet", 51),
            CourseGate.Line(pick.Gate, 5, "ba:street_thirdstreet", 51));
    }

    [Fact]
    public void Course_wins_when_expansion_is_also_closed()
    {
        var notReady = new[]
        {
            new StoreHealth(true, true, false, true, true, "ba:street_seventhavenue 15"),
        };
        var pick = FitOut.Decide(
            notReady,
            "ba:street_thirdstreet 51",
            FitOutProgress.Bare(),
            "Market Demands");

        Assert.False(pick.Chosen);
        Assert.Equal("market-demands", pick.Gate);
        Assert.NotEqual(CourseGate.ExpansionSlug, pick.Gate);
    }

    [Fact]
    public void First_lot_and_a_fitted_lot_stay_quiet()
    {
        var ready = new[] { StoreHealth.Healthy("ba:street_seventhavenue 15") };
        var only = FitOut.Decide(Array.Empty<StoreHealth>(), "ba:street_thirdstreet 51", FitOutProgress.Bare(), "Market Demands");
        var fitted = FitOut.Decide(ready, "ba:street_thirdstreet 51", FitOutProgress.Fitted(), "Market Demands");
        var blankPlace = FitOut.Decide(ready, "", FitOutProgress.Bare(), "Market Demands");
        var open = FitOut.Decide(ready, "ba:street_thirdstreet 51", FitOutProgress.Bare());

        Assert.Equal("", only.Gate);
        Assert.Equal("", fitted.Gate);
        Assert.Equal("", blankPlace.Gate);
        Assert.False(only.Chosen);
        Assert.False(fitted.Chosen);
        Assert.True(open.Chosen);
        Assert.Equal("layout", open.Step);
        Assert.Equal("", open.Gate);
        Assert.Equal("", CourseGate.Slug(null));
        Assert.Equal("", CourseGate.Slug("   "));
        Assert.Equal("", CourseGate.Line("", 5, "ba:street_thirdstreet", 51));

        var started = FitOut.Decide(
            new[] { new StoreHealth(true, true, false, true, true, "ba:street_seventhavenue 15") },
            "ba:street_thirdstreet 51",
            new FitOutProgress(true, false, false, false, false),
            "Market Demands");
        Assert.False(started.Chosen);
        Assert.Equal("", started.Gate);
    }

    [Fact]
    public void Record_writes_the_course_line_once()
    {
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-course-gate-" + Guid.NewGuid().ToString("N")));
        try
        {
            var first = CourseGate.Record(dir.FullName, "market-demands", 5, "ba:street_thirdstreet", 51);
            var again = CourseGate.Record(dir.FullName, "market-demands", 5, "ba:street_thirdstreet", 51);
            var nextDay = CourseGate.Record(dir.FullName, "market-demands", 6, "ba:street_thirdstreet", 51);
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));
            var hits = 0;
            foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line == first)
                {
                    hits++;
                }
            }

            Assert.Contains("course=market-demands", first);
            Assert.Equal(first, again);
            Assert.Equal(1, hits);
            Assert.Contains(nextDay, text);
            Assert.Contains("day=6", nextDay);
        }
        finally
        {
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Chain_bare_rent_fitout_decide_course_block()
    {
        // Chain: bare rent → fit-out decide → course or expansion block
        var lot = new BareLeaseFact(true, "", "ba:street_thirdstreet", 51);
        Assert.True(BareLease.IsBare(lot));
        Assert.Equal("other", SpendKind.Of(lot.BusinessType));

        var place = lot.Street + " " + lot.Number.ToString();
        var pick = FitOut.Decide(
            new[] { StoreHealth.Healthy("ba:street_seventhavenue 15") },
            place,
            FitOutProgress.Bare(),
            "Market Demands");
        var line = CourseGate.Line(pick.Gate, 5, lot.Street, lot.Number);

        Assert.Equal("market-demands", pick.Gate);
        Assert.False(pick.Chosen);
        Assert.StartsWith("course.needed version=" + LoadLog.Version, line);
        Assert.Contains("day=5", line);
        Assert.Contains("street=" + lot.Street, line);
        Assert.Contains("number=" + lot.Number.ToString(), line);
    }

    [Fact]
    public void Chain_bare_rent_fitout_decide_expansion_block()
    {
        // Chain: bare rent → fit-out decide → course or expansion block
        var lot = new BareLeaseFact(true, "", "ba:street_thirdstreet", 51);
        Assert.True(BareLease.IsBare(lot));

        var priors = new[]
        {
            new StoreHealth(true, true, false, true, true, "ba:street_seventhavenue 15"),
        };
        var place = lot.Street + " " + lot.Number.ToString();
        var refused = FitOut.Decide(priors, place, FitOutProgress.Bare());
        var blocked = CourseGate.Line(refused.Gate, 5, lot.Street, lot.Number);

        Assert.Equal(CourseGate.ExpansionSlug, refused.Gate);
        Assert.Contains("block.named", blocked);
        Assert.Contains("slug=expansion-blocked", blocked);
        Assert.Contains("where=fitout", blocked);
        Assert.Contains("number=51", blocked);

        var both = FitOut.Decide(priors, place, FitOutProgress.Bare(), "Market Demands");
        var winner = CourseGate.Line(both.Gate, 5, lot.Street, lot.Number);
        Assert.Contains("course=market-demands", winner);
        Assert.DoesNotContain("slug=expansion-blocked", winner);
    }

    [Fact]
    public void Open_store_does_not_hide_a_named_bare_lot()
    {
        // Harvest: 1.18.5 days 9–34, 19 9th Avenue kind=other, no course.needed.
        var lot = new BareLeaseFact(true, "", "ba:street_ninthavenue", 19);
        Assert.True(BareLease.IsBare(lot));
        Assert.Equal("other", SpendKind.Of(lot.BusinessType));

        var owned = new[]
        {
            StoreHealth.Healthy("ba:street_seventhavenue 15"),
            new StoreHealth(false, false, false, false, false, "ba:street_ninthavenue 19"),
        };
        var sites = new[]
        {
            new FitOutSite(
                "ba:street_seventhavenue",
                15,
                bare: false,
                new FitOutProgress(true, false, false, false, false)),
            new FitOutSite(
                lot.Street,
                lot.Number,
                bare: true,
                new FitOutProgress(true, false, false, false, false)),
        };

        var pick = FitOut.BareLot(owned, sites, "Market Demands");
        Assert.Equal("ba:street_ninthavenue 19", pick.Place);
        Assert.Equal("market-demands", pick.Gate);
        Assert.False(pick.Chosen);
        Assert.Contains(
            "number=19",
            CourseGate.Line(pick.Gate, 10, lot.Street, lot.Number));
    }

    [Fact]
    public void Chain_open_store_bare_rent_names_the_course()
    {
        // Chain: bare rent → fit-out decide → course or expansion block
        var lot = new BareLeaseFact(true, "", "ba:street_ninthavenue", 19);
        Assert.True(BareLease.IsBare(lot));
        var place = lot.Street + " " + lot.Number.ToString();
        var owned = new[]
        {
            new StoreHealth(true, true, false, true, true, "ba:street_seventhavenue 15"),
            new StoreHealth(false, false, false, false, false, place),
        };
        var sites = new[]
        {
            new FitOutSite("ba:street_seventhavenue", 15, false, FitOutProgress.Fitted()),
            new FitOutSite(lot.Street, lot.Number, true, FitOutProgress.Bare()),
        };

        var blocked = FitOut.BareLot(owned, sites, "");
        var named = FitOut.BareLot(owned, sites, "Market Demands");
        Assert.Equal(CourseGate.ExpansionSlug, blocked.Gate);
        Assert.Equal("market-demands", named.Gate);
        Assert.Contains("course=market-demands", CourseGate.Line(named.Gate, 10, lot.Street, lot.Number));
        Assert.DoesNotContain("slug=expansion-blocked", CourseGate.Line(named.Gate, 10, lot.Street, lot.Number));
    }

    [Fact]
    public void Empty_business_type_is_the_bare_second_lot()
    {
        // Harvest: 19 9th Avenue shows Empty (Retail), kind=other, and no gate line.
        var lot = new BareLeaseFact(true, BareLease.EmptyType, "ba:street_ninthavenue", 19);
        Assert.True(BareLease.IsBare(lot));
        Assert.Equal("other", SpendKind.Of(lot.BusinessType));
        Assert.False(BareLease.IsBare(new BareLeaseFact(true, "ba:businesstype_giftshop", lot.Street, 15)));

        var owned = new[] { StoreHealth.Healthy("ba:street_seventhavenue 15") };
        var sites = new[]
        {
            new FitOutSite("ba:street_seventhavenue", 15, bare: false, FitOutProgress.Fitted()),
            new FitOutSite(lot.Street, lot.Number, bare: true, new FitOutProgress(true, false, false, false, false)),
        };
        var pick = FitOut.BareLot(owned, sites, "Market Demands");
        Assert.Equal("ba:street_ninthavenue 19", pick.Place);
        Assert.Equal("market-demands", pick.Gate);
        Assert.False(pick.Chosen);
    }

    [Fact]
    public void Chain_empty_type_bare_rent_names_the_gate()
    {
        // Chain: bare rent → fit-out decide → course or expansion block
        var lot = new BareLeaseFact(true, BareLease.EmptyType, "ba:street_ninthavenue", 19);
        Assert.True(BareLease.IsBare(lot));
        Assert.Equal("other", SpendKind.Of(lot.BusinessType));

        var owned = new[]
        {
            new StoreHealth(true, true, false, true, true, "ba:street_seventhavenue 15"),
        };
        var sites = new[]
        {
            new FitOutSite("ba:street_seventhavenue", 15, false, FitOutProgress.Fitted()),
            new FitOutSite(lot.Street, lot.Number, BareLease.IsEmptyType(lot.BusinessType), FitOutProgress.Bare()),
        };
        var blocked = FitOut.BareLot(owned, sites, "");
        var named = FitOut.BareLot(owned, sites, "Market Demands");
        Assert.Equal(CourseGate.ExpansionSlug, blocked.Gate);
        Assert.Equal("market-demands", named.Gate);
        Assert.Contains("number=19", CourseGate.Line(named.Gate, 35, lot.Street, lot.Number));
        Assert.Contains("slug=expansion-blocked", CourseGate.Line(blocked.Gate, 35, lot.Street, lot.Number));
    }
}
