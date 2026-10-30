using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class GameApiTests
{
    [Fact]
    public void GameApi_locks_the_four_calls()
    {
        var note = File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md"));
        var api = GameApi.Read(OneRow(), note);

        Assert.Equal("3682", api.Build);
        Assert.Equal("Helpers.BusinessHelper.ShutdownBusiness", api.Close);
        Assert.Equal("BizManPresentation.SendBuyBuildingOffer", api.Buy);
        Assert.Equal("Buildings.Office.Headquarters.PricingManagerPlan.SetPrice", api.Price);
        Assert.Equal(
            "UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination",
            api.Delivery);
    }

    [Fact]
    public void GameApi_a_near_miss_is_not_the_call()
    {
        var note = File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md"));
        var api = GameApi.Read(OneRow(), note);

        Assert.NotEqual("BuildingRegistration.TemporarilyClose", api.Close);
        Assert.NotEqual("Buildings.BuildingCommands.Command_RentBuilding", api.Buy);
        Assert.NotEqual("Buildings.Office.Headquarters.PricingManagerPlan.ApplyManualPrice", api.Price);
        Assert.NotEqual(
            "UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagersPlanList.AddPlan",
            api.Delivery);
        Assert.NotEqual(api.Close, api.Buy);
        Assert.NotEqual(api.Price, api.Delivery);
    }

    [Fact]
    public void GameApi_a_missing_action_is_not_a_call()
    {
        var note = """
            # Game API

            Build 3682.

            | action | call |
            | --- | --- |
            | close | Helpers.BusinessHelper.ShutdownBusiness |
            """;

        var api = GameApi.Read(OneRow(), note);

        Assert.Equal("3682", api.Build);
        Assert.Equal("Helpers.BusinessHelper.ShutdownBusiness", api.Close);
        Assert.Equal("", api.Buy);
        Assert.Equal("", api.Price);
        Assert.Equal("", api.Delivery);
    }

    [Fact]
    public void GameApi_a_different_build_stays()
    {
        var note = """
            # Game API

            Build 1000.

            | action | call |
            | --- | --- |
            | close | Helpers.BusinessHelper.ShutdownBusiness |
            | buy | BizManPresentation.SendBuyBuildingOffer |
            | price | Buildings.Office.Headquarters.PricingManagerPlan.SetPrice |
            | delivery | UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination |
            """;

        var api = GameApi.Read(OneRow(), note);

        Assert.Equal("1000", api.Build);
        Assert.Equal("Helpers.BusinessHelper.ShutdownBusiness", api.Close);
    }

    [Fact]
    public void GameApi_a_tie_keeps_the_later_call()
    {
        var note = """
            # Game API

            Build 3682.

            | action | call |
            | --- | --- |
            | close | BuildingRegistration.TemporarilyClose |
            | close | Helpers.BusinessHelper.ShutdownBusiness |
            """;

        var api = GameApi.Read(OneRow(), note);

        Assert.Equal("Helpers.BusinessHelper.ShutdownBusiness", api.Close);
        Assert.NotEqual("BuildingRegistration.TemporarilyClose", api.Close);
    }

    [Fact]
    public void GameApi_an_empty_panel_has_no_calls()
    {
        var note = File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md"));

        var api = GameApi.Read(Array.Empty<PanelLine>(), note);

        Assert.Equal("", api.Build);
        Assert.Equal("", api.Close);
        Assert.Equal("", api.Buy);
        Assert.Equal("", api.Price);
        Assert.Equal("", api.Delivery);
    }

    private static PanelLine[] OneRow()
    {
        return new[] { new PanelLine("red", "Low", "ba:street_a", 1, "Close Low.") };
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "script", "game_build.py")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("repo root not found");
    }
}
