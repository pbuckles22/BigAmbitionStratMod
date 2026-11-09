using ChaoticCEO;

namespace ChaoticCEO.Tests;

[Collection(nameof(AutopilotState))]
public class ControlsTests
{
    [Fact]
    public void Controls_execute_names_each_roadmap_step()
    {
        var lines = new[]
        {
            new PanelLine("close", "step", "ba:street_fourthavenue", 28, "Close Fourth Avenue 28."),
            new PanelLine("open", "step", "ba:street_fifthavenue", 36, "Open Fifth Avenue 36."),
            new PanelLine("move", "logistics", "ba:street_side", 9, "Move the store."),
        };

        var buttons = Controls.Execute(lines);
        var close = buttons[0];
        var open = buttons[1];

        Assert.Equal(2, buttons.Count);
        Assert.Equal("Execute", close.Label);
        Assert.Equal("close", close.Kind);
        Assert.Equal("ba:street_fourthavenue", close.Street);
        Assert.Equal(28, close.Number);
        Assert.Equal(
            "action=execute kind=close street=ba:street_fourthavenue number=28",
            Controls.Press(close));
        Assert.Equal("Execute", open.Label);
        Assert.Equal("open", open.Kind);
        Assert.Equal("ba:street_fifthavenue", open.Street);
        Assert.Equal(36, open.Number);
        Assert.DoesNotContain(buttons, button => button.Kind == "move");
    }

    [Fact]
    public void Controls_execute_all_is_the_7_12_button()
    {
        var row = Controls.Row(autopilot: true, dryRun: false);
        var buttons = Controls.Buttons(autopilot: true, dryRun: false);

        Assert.Equal("Execute all", Controls.ExecuteAll);
        Assert.Equal("7.12", Controls.ExecuteAllStory);
        Assert.Equal("Execute all", buttons[2]);
        Assert.EndsWith("Execute all", row);
        Assert.Equal("action=execute-all story=7.12", Controls.PressAll());
    }

    [Fact]
    public void Controls_toggle_sets_autopilot_and_the_bar_follows()
    {
        Autopilot.Enabled = true;
        Autopilot.DryRun = false;
        try
        {
            var off = Controls.Toggle();
            var offBar = TopBar.Line(1016, 1m, 2m, 3m, 1, Autopilot.Enabled, 1);
            var offButtons = Controls.Buttons(Autopilot.Enabled, Autopilot.DryRun);
            var on = Controls.Toggle();
            var onBar = TopBar.Line(1016, 1m, 2m, 3m, 1, Autopilot.Enabled, 1);

            Assert.False(off);
            Assert.True(on);
            Assert.True(Autopilot.Enabled);
            Assert.Contains("Autopilot off", offBar);
            Assert.DoesNotContain("Autopilot on", offBar);
            Assert.Equal("Autopilot off", offButtons[0]);
            Assert.Contains("Autopilot on", onBar);
            Assert.DoesNotContain("Autopilot off", onBar);
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Controls_dry_run_sets_the_flag_and_names_it()
    {
        Autopilot.Enabled = true;
        Autopilot.DryRun = false;
        try
        {
            var on = Controls.Dry();
            var row = Controls.Row(Autopilot.Enabled, Autopilot.DryRun);
            var buttons = Controls.Buttons(Autopilot.Enabled, Autopilot.DryRun);

            Assert.True(on);
            Assert.True(Autopilot.DryRun);
            Assert.True(Autopilot.Enabled);
            Assert.Contains("Dry run on", row);
            Assert.Contains("Autopilot on", row);
            Assert.Equal("Dry run on", buttons[1]);
            Assert.EndsWith("Execute all", row);
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }

    [Fact]
    public void Controls_f10_stays_off_and_an_empty_roadmap_still_has_execute_all()
    {
        Autopilot.Enabled = false;
        Autopilot.DryRun = true;
        try
        {
            var paused = Controls.Pause();
            var again = Controls.Pause();
            var buttons = Controls.Execute(null);
            var row = Controls.Row(Autopilot.Enabled, dryRun: false);
            var captions = Controls.Buttons(Autopilot.Enabled, dryRun: false);

            Assert.False(paused);
            Assert.False(again);
            Assert.False(Autopilot.Enabled);
            Assert.True(Autopilot.DryRun);
            Assert.Empty(buttons);
            Assert.Equal("Autopilot off", captions[0]);
            Assert.Equal("Execute all", captions[2]);
            Assert.Equal("Autopilot off  Dry run off  Execute all", row);
        }
        finally
        {
            Autopilot.Enabled = true;
            Autopilot.DryRun = false;
        }
    }
}
