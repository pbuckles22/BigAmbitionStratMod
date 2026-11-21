using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class SagaTests
{
    [Fact]
    public void Earlier_day_logs_TIMELINE_FORK_with_both_days()
    {
        var line = Saga.Fork(5, 2);
        Assert.Equal(
            "TIMELINE_FORK version=" + LoadLog.Version + " fromDay=5 toDay=2",
            line);
    }

    [Fact]
    public void Load_of_an_earlier_day_appends_one_TIMELINE_FORK()
    {
        var dir = Temp("fork");
        try
        {
            Saga.Reset();
            Assert.Equal("", Saga.Load(dir.FullName, 5));
            var fork = Saga.Load(dir.FullName, 2);
            var again = Saga.Load(dir.FullName, 2);
            var lines = File.ReadAllLines(LoadLog.CeoLogPath(dir.FullName));

            Assert.Equal("TIMELINE_FORK version=" + LoadLog.Version + " fromDay=5 toDay=2", fork);
            Assert.Equal("", again);
            Assert.Equal(1, lines.Count(line => line.StartsWith("TIMELINE_FORK ", StringComparison.Ordinal)));
            Assert.Contains("fromDay=5", fork);
            Assert.Contains("toDay=2", fork);
        }
        finally
        {
            Saga.Reset();
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Later_day_does_not_fork()
    {
        var dir = Temp("forward");
        try
        {
            Saga.Reset();
            Assert.Equal("", Saga.Load(dir.FullName, 2));
            Assert.Equal("", Saga.Load(dir.FullName, 5));
            Assert.False(File.Exists(LoadLog.CeoLogPath(dir.FullName)));
        }
        finally
        {
            Saga.Reset();
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Same_day_does_not_fork()
    {
        Saga.Reset();
        try
        {
            Assert.Equal("", Saga.Note(4));
            Assert.Equal("", Saga.Note(4));
        }
        finally
        {
            Saga.Reset();
        }
    }

    [Fact]
    public void First_day_does_not_fork()
    {
        Saga.Reset();
        try
        {
            Assert.Equal("", Saga.Note(1));
        }
        finally
        {
            Saga.Reset();
        }
    }

    [Fact]
    public void Day_zero_does_not_move_the_watermark()
    {
        var dir = Temp("zero");
        try
        {
            Saga.Reset();
            Assert.Equal("", Saga.Load(dir.FullName, 0));
            Assert.Equal("", Saga.Load(dir.FullName, 2));
            var fork = Saga.Load(dir.FullName, 1);
            Assert.Equal("TIMELINE_FORK version=" + LoadLog.Version + " fromDay=2 toDay=1", fork);
        }
        finally
        {
            Saga.Reset();
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Advance_after_a_fork_forks_from_the_new_day()
    {
        var dir = Temp("refork");
        try
        {
            Saga.Reset();
            Assert.Equal("", Saga.Load(dir.FullName, 5));
            var first = Saga.Load(dir.FullName, 2);
            Assert.Equal("", Saga.Load(dir.FullName, 4));
            var second = Saga.Load(dir.FullName, 1);
            var lines = File.ReadAllLines(LoadLog.CeoLogPath(dir.FullName));

            Assert.Contains("fromDay=5", first);
            Assert.Contains("toDay=2", first);
            Assert.Equal("TIMELINE_FORK version=" + LoadLog.Version + " fromDay=4 toDay=1", second);
            Assert.Equal(2, lines.Count(line => line.StartsWith("TIMELINE_FORK ", StringComparison.Ordinal)));
        }
        finally
        {
            Saga.Reset();
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void A_new_process_still_forks_from_the_saved_day()
    {
        var dir = Temp("fresh");
        try
        {
            Saga.Reset();
            Assert.Equal("", Saga.Load(dir.FullName, 5));
            Saga.Reset();
            var fork = Saga.Load(dir.FullName, 2);
            Assert.Equal("TIMELINE_FORK version=" + LoadLog.Version + " fromDay=5 toDay=2", fork);
        }
        finally
        {
            Saga.Reset();
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Day_9_lease_and_day_14_open_share_one_saga_id()
    {
        var lease = Saga.Step("lease", 9, "ba:street_seventhavenue", 15);
        var open = Saga.Step("open", 14, "ba:street_seventhavenue", 15);
        Assert.Contains("stage=lease", lease);
        Assert.Contains("day=9", lease);
        Assert.Contains("stage=open", open);
        Assert.Contains("day=14", open);
        Assert.Equal(Field(lease, "saga_id"), Field(open, "saga_id"));
        Assert.Equal("ba:street_seventhavenue-15", Field(lease, "saga_id"));
    }

    [Fact]
    public void Two_addresses_do_not_share_a_saga_id()
    {
        var seventh = Saga.Step("lease", 1, "ba:street_seventhavenue", 15);
        var ninth = Saga.Step("lease", 1, "ba:street_ninthavenue", 19);
        Assert.NotEqual(Field(seventh, "saga_id"), Field(ninth, "saga_id"));
        Assert.Equal("ba:street_ninthavenue-19", Field(ninth, "saga_id"));
    }

    [Fact]
    public void Blank_address_is_not_a_saga()
    {
        Assert.Equal("", Saga.Step("lease", 1, "", 15));
        Assert.Equal("", Saga.Step("lease", 1, "ba:street_seventhavenue", 0));
        Assert.Equal("", Saga.Step("lease", 1, "  ", 15));
    }

    [Fact]
    public void Unknown_stage_is_not_a_saga()
    {
        Assert.Equal("", Saga.Step("breaker", 1, "ba:street_seventhavenue", 15));
        Assert.Equal("", Saga.Step("", 1, "ba:street_seventhavenue", 15));
    }

    [Fact]
    public void A_repeated_stage_is_written_once()
    {
        var dir = Temp("once");
        try
        {
            Saga.Reset();
            var first = Saga.Record(dir.FullName, "lease", 9, "ba:street_seventhavenue", 15);
            var second = Saga.Record(dir.FullName, "lease", 14, "ba:street_seventhavenue", 15);
            var lines = File.ReadAllLines(LoadLog.CeoLogPath(dir.FullName));

            Assert.Equal(first, second);
            Assert.Contains("day=9", first);
            Assert.Equal(1, lines.Count(line => line.Contains("stage=lease", StringComparison.Ordinal)));
        }
        finally
        {
            Saga.Reset();
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void A_skipped_layout_does_not_write_a_saga()
    {
        var dir = Temp("skip");
        try
        {
            Saga.Reset();
            var skipped = "layout.skipped version=" + LoadLog.Version + " day=2 reason=open";
            Assert.Equal(skipped, Saga.After(dir.FullName, skipped, 2));
            Assert.False(File.Exists(LoadLog.CeoLogPath(dir.FullName)));
        }
        finally
        {
            Saga.Reset();
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void A_zero_day_keeps_the_watermark()
    {
        var dir = Temp("hold");
        try
        {
            Saga.Reset();
            Assert.Equal("", Saga.Load(dir.FullName, 5));
            Assert.Equal("", Saga.Load(dir.FullName, 0));
            var fork = Saga.Load(dir.FullName, 2);
            Assert.Equal("TIMELINE_FORK version=" + LoadLog.Version + " fromDay=5 toDay=2", fork);
        }
        finally
        {
            Saga.Reset();
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Chain_lease_then_fitout_keeps_one_saga_id()
    {
        // Chain: lease → fit-out → saga
        var dir = Temp("chain");
        try
        {
            Saga.Reset();
            var rent = Rent.Choose(
                new[] { new RentLot("ba:street_seventhavenue", 15, 40m) },
                1000m,
                false,
                GameApi.Live());
            var leased = Rent.Record(dir.FullName, rent, 9);
            var pick = Starter.Choose(Starter.StageOne(), false, GameApi.Live());
            var loaded = Starter.Record(dir.FullName, pick, rent, 10);
            var saga = Saga.Follow(dir.FullName, leased, loaded);
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));
            var leaseSaga = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Single(line => line.Contains("stage=lease", StringComparison.Ordinal));

            Assert.StartsWith("building.rented ", leased);
            Assert.StartsWith("layout.loaded ", loaded);
            Assert.Contains("street=" + Field(leased, "street"), loaded);
            Assert.Contains("number=" + Field(leased, "number"), loaded);
            Assert.StartsWith("saga version=" + LoadLog.Version, saga);
            Assert.Contains("stage=fit-out", saga);
            Assert.Contains("day=10", saga);
            Assert.Equal(Field(leaseSaga, "saga_id"), Field(saga, "saga_id"));
            Assert.Equal("ba:street_seventhavenue-15", Field(saga, "saga_id"));
            Assert.Contains("day=9", leaseSaga);
            Assert.Contains(saga, text);
        }
        finally
        {
            Saga.Reset();
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Chain_lease_through_open_keeps_one_saga_id()
    {
        // Chain: lease → fit-out → staff → stock → open
        var dir = Temp("stages");
        try
        {
            Saga.Reset();
            var rent = Rent.Choose(
                new[] { new RentLot("ba:street_seventhavenue", 15, 40m) },
                1000m,
                false,
                GameApi.Live());
            var leased = Rent.Record(dir.FullName, rent, 9);
            var pick = Starter.Choose(Starter.StageOne(), false, GameApi.Live());
            var loaded = Starter.Record(dir.FullName, pick, rent, 10);
            var fit = Saga.Follow(dir.FullName, leased, loaded);
            var staffed = StoreOps.Record(
                dir.FullName,
                StoreOps.Assign(pick, StoreOps.Opening(), GameApi.Live()),
                rent,
                11);
            var signed = Wholesaler.Record(
                dir.FullName,
                Wholesaler.Sign(rent, pick, Wholesaler.StageOne(), false, GameApi.Live()),
                12);
            var health = new StoreHealth(
                true,
                true,
                true,
                true,
                true,
                rent.Street + " " + rent.Number.ToString(),
                14,
                100m,
                new[] { 10m });
            var opened = StoreHealth.Record(dir.FullName, health);
            Saga.After(dir.FullName, staffed, 11);
            Saga.After(dir.FullName, signed, 12);
            Saga.After(dir.FullName, opened, 14);
            var lines = File.ReadAllLines(LoadLog.CeoLogPath(dir.FullName))
                .Where(line => line.StartsWith("saga ", StringComparison.Ordinal))
                .ToArray();
            var id = Field(fit, "saga_id");

            Assert.Equal(5, lines.Length);
            Assert.Contains("stage=lease", lines[0]);
            Assert.Contains("day=9", lines[0]);
            Assert.Contains("stage=fit-out", lines[1]);
            Assert.Contains("stage=staff", lines[2]);
            Assert.Contains("day=11", lines[2]);
            Assert.Contains("stage=stock", lines[3]);
            Assert.Contains("day=12", lines[3]);
            Assert.Contains("stage=open", lines[4]);
            Assert.Contains("day=14", lines[4]);
            Assert.All(lines, line => Assert.Equal(id, Field(line, "saga_id")));
            Assert.StartsWith("ops.assigned ", staffed);
            Assert.StartsWith("contract.signed ", signed);
            Assert.Contains("g1=true", opened);
        }
        finally
        {
            Saga.Reset();
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Chain_saga_then_an_earlier_load_names_both_days()
    {
        // Chain: lease → fit-out → saga → fork
        var dir = Temp("fork-chain");
        try
        {
            Saga.Reset();
            var rent = Rent.Choose(
                new[] { new RentLot("ba:street_seventhavenue", 15, 40m) },
                1000m,
                false,
                GameApi.Live());
            var leased = Rent.Record(dir.FullName, rent, 9);
            var pick = Starter.Choose(Starter.StageOne(), false, GameApi.Live());
            var loaded = Starter.Record(dir.FullName, pick, rent, 10);
            var saga = Saga.Follow(dir.FullName, leased, loaded);
            var seen = int.Parse(Field(leased, "day"), System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal("", Saga.Load(dir.FullName, seen));
            var fork = Saga.Load(dir.FullName, 2);
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));

            Assert.Contains("saga_id=" + Field(saga, "saga_id"), text);
            Assert.Equal(
                "TIMELINE_FORK version=" + LoadLog.Version + " fromDay=9 toDay=2",
                fork);
            Assert.Equal(1, text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Count(line => line.StartsWith("TIMELINE_FORK ", StringComparison.Ordinal)));
        }
        finally
        {
            Saga.Reset();
            Directory.Delete(dir.FullName, true);
        }
    }

    [Fact]
    public void Chain_closed_health_does_not_open_the_saga()
    {
        // Chain: lease → fit-out → saga → closed
        var dir = Temp("closed");
        try
        {
            Saga.Reset();
            var rent = Rent.Choose(
                new[] { new RentLot("ba:street_seventhavenue", 15, 40m) },
                1000m,
                false,
                GameApi.Live());
            var leased = Rent.Record(dir.FullName, rent, 9);
            var pick = Starter.Choose(Starter.StageOne(), false, GameApi.Live());
            var loaded = Starter.Record(dir.FullName, pick, rent, 10);
            var saga = Saga.Follow(dir.FullName, leased, loaded);
            var closed = StoreHealth.Record(
                dir.FullName,
                new StoreHealth(false, false, false, false, false, rent.Street + " " + rent.Number));
            Assert.Equal(closed, Saga.After(dir.FullName, closed, 14));
            var before = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));
            Assert.DoesNotContain("stage=open", before);
            var opened = StoreHealth.Record(
                dir.FullName,
                new StoreHealth(true, true, true, true, true, rent.Street + " " + rent.Number));
            Saga.After(dir.FullName, opened, 14);
            var text = File.ReadAllText(LoadLog.CeoLogPath(dir.FullName));
            var open = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Single(line => line.Contains("stage=open", StringComparison.Ordinal));

            Assert.Contains("g1=false", closed);
            Assert.Contains("g1=true", opened);
            Assert.Equal(Field(saga, "saga_id"), Field(open, "saga_id"));
            Assert.Contains("day=14", open);
        }
        finally
        {
            Saga.Reset();
            Directory.Delete(dir.FullName, true);
        }
    }

    private static DirectoryInfo Temp(string name)
    {
        return Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "ceo-saga-" + name + "-" + Guid.NewGuid().ToString("N")));
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
