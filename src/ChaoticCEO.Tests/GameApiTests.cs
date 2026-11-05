using ChaoticCEO;

namespace ChaoticCEO.Tests;

public class GameApiTests
{
    [Fact]
    public void GameApi_locks_the_calls()
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
        Assert.Equal(
            "BusinessLayoutSets.BusinessLayoutSetHelper.LoadBusinessLayoutSet",
            api.Clone);
        Assert.NotEqual("BusinessLayoutSets.BusinessLayoutSetHelper.SetBusinessLayout", api.Clone);
        Assert.NotEqual("BuildingManager.ApplyInteriorDesign", api.Clone);
        Assert.NotEqual("ItemHelper.Command_CloneItem", api.Clone);
        Assert.NotEqual(api.Buy, api.Clone);

        var live = GameApi.Live();
        Assert.Equal(api.Build, live.Build);
        Assert.Equal(api.Close, live.Close);
        Assert.Equal(api.Buy, live.Buy);
        Assert.Equal(api.Price, live.Price);
        Assert.Equal(api.Delivery, live.Delivery);
        Assert.Equal(api.Clone, live.Clone);
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
        Assert.Equal("", api.Clone);
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
    public void GameApi_records_each_epic_11_call()
    {
        var note = File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md"));
        var api = GameApi.Read(OneRow(), note);

        var live = GameApi.Live();
        Assert.Equal("Buildings.ContractVehicleForSale.Order", api.Call("truck buy"));
        Assert.Equal("Entities.VehicleSlot.AssignVehicle", api.Call("slot assign"));
        Assert.Equal("Helpers.EmployeeHelper.HireCandidate", api.Call("hire"));
        Assert.Equal("Buildings.Office.Headquarters.HrManagerPlan.AssignEmployee", api.Call("plan assign"));
        Assert.Equal(
            "UI.Smartphone.Apps.BizMan.PurchasingAgent.PurchasingAgentProductCellView.ChangeAssignedWarehouse",
            api.Call("purchasing plan"));
        Assert.Equal(
            "UI.Smartphone.Apps.BizMan.PricingManagers.PricingManagersPlanList.AddPlan",
            api.Call("pricing plan create"));
        Assert.Equal("Buildings.Office.Headquarters.HrManagerPlan.TrainEmployees", api.Call("staff train"));
        Assert.Equal(
            "UI.Smartphone.Apps.BizMan.PurchasingAgent.PurchasingAgentPlanUI.StartOrder",
            api.Call("importer order"));
        Assert.Equal("Entities.DeliveryContract.Remove", api.Call("wholesaler contract drop"));
        Assert.NotEqual("Buildings.ContractVehicleForSale.Purchase", api.Call("truck buy"));
        Assert.NotEqual("UI.PurchaseVehicle.PurchaseVehicleUI.Purchase", api.Call("truck buy"));
        Assert.NotEqual("UI.Smartphone.Apps.BizMan.Warehouse.DriverStation.SetUp", api.Call("slot assign"));
        Assert.NotEqual("Entities.Warehouse.AssignParkedVehiclesToFreeSlots", api.Call("slot assign"));
        Assert.NotEqual("CandidateCellView.Hire", api.Call("hire"));
        Assert.NotEqual(
            "UI.Smartphone.Apps.BizMan.HRManagers.HrManagersPlanList.ChangeHrManager",
            api.Call("plan assign"));
        Assert.NotEqual(
            "UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagersPlanList.ChangeLogisticsManager",
            api.Call("plan assign"));
        Assert.NotEqual(
            "UI.Smartphone.Apps.BizMan.Headhunters.HeadhuntersPlanList.ChangeHeadhunter",
            api.Call("plan assign"));
        Assert.NotEqual("Buildings.Office.Headquarters.PricingManagerPlan.AssignEmployee", api.Call("plan assign"));
        Assert.NotEqual(api.Call("importer order"), api.Call("purchasing plan"));
        Assert.NotEqual(
            "UI.Smartphone.Apps.BizMan.PurchasingAgent.PurchasingAgentProductsMassActionsUI.MassDesignateWarehouse",
            api.Call("purchasing plan"));
        Assert.NotEqual("Buildings.Office.Headquarters.PricingManagerHelper.AddPlan", api.Call("pricing plan create"));
        Assert.NotEqual("UI.Smartphone.Apps.BizMan.HRManagers.HrManagerPlanUI.LoadPlan", api.Call("staff train"));
        Assert.NotEqual("Entities.ImportPartnership.DoDeliveries", api.Call("importer order"));
        Assert.NotEqual(
            "UI.Smartphone.Apps.BizMan.PurchasingAgent.PurchasingAgentPlanUI.OnRepeatingOrderToggleValueChanged",
            api.Call("importer order"));
        Assert.NotEqual("UI.Smartphone.Apps.BizMan.BizManContractSettings.EndContract", api.Call("wholesaler contract drop"));
        Assert.NotEqual("UI.Smartphone.Apps.BizMan.BizManContractSettings.CancelContract", api.Call("wholesaler contract drop"));
        foreach (var action in Epic11Actions)
        {
            Assert.Equal(api.Call(action), live.Call(action));
        }
    }

    [Fact]
    public void GameApi_prose_is_not_a_recorded_call()
    {
        var note = """
            # Game API

            Build 3682.

            | action | call |
            | --- | --- |
            | truck buy | Buy a freight truck and park it in a warehouse garage slot |
            """;

        var api = GameApi.Read(OneRow(), note);

        Assert.Equal("", api.Call("truck buy"));
    }

    [Fact]
    public void GameApi_a_later_method_wins_over_prose()
    {
        var note = """
            # Game API

            Build 3682.

            | action | call |
            | --- | --- |
            | truck buy | Buy a freight truck and park it in a warehouse garage slot |
            | truck buy | Vehicles.Garage.FreightBuyer.BuyFreightTruck |
            """;

        var api = GameApi.Read(OneRow(), note);

        Assert.Equal("Vehicles.Garage.FreightBuyer.BuyFreightTruck", api.Call("truck buy"));
    }

    [Fact]
    public void GameApi_an_unverified_neighbor_is_not_a_call()
    {
        var note = File.ReadAllText(Path.Combine(RepoRoot(), "doc", "requirements", "game-api.md"));
        var api = GameApi.Read(OneRow(), note);

        Assert.Equal("", api.Call("staff plan write"));
        Assert.Equal("", api.Call("rent"));
        Assert.Equal("", api.Call("wholesaler contract"));
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
        Assert.Equal("", api.Clone);
        Assert.Equal("", api.Call("truck buy"));
    }

    private static readonly string[] Epic11Actions =
    [
        "truck buy",
        "slot assign",
        "hire",
        "plan assign",
        "purchasing plan",
        "pricing plan create",
        "staff train",
        "importer order",
        "wholesaler contract drop",
    ];

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
