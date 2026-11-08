using System;
using System.Collections.Generic;

namespace ChaoticCEO
{
    public sealed class ApiNote
    {
        public ApiNote(
            string build,
            string close,
            string buy,
            string price,
            string delivery,
            string clone = "",
            Dictionary<string, string>? recorded = null)
        {
            Build = build ?? "";
            Close = close ?? "";
            Buy = buy ?? "";
            Price = price ?? "";
            Delivery = delivery ?? "";
            Clone = clone ?? "";
            this.recorded = recorded;
        }

        public string Build { get; }

        public string Close { get; }

        public string Buy { get; }

        public string Price { get; }

        public string Delivery { get; }

        public string Clone { get; }

        public string Call(string action)
        {
            if (action == null || recorded == null || !recorded.TryGetValue(action, out var call) || call == null)
            {
                return "";
            }

            if (!IsRecordedCall(call))
            {
                return "";
            }

            return call;
        }

        private static bool IsRecordedCall(string call)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(
                call,
                "^[A-Za-z_][A-Za-z0-9_]*(?:\\.[A-Za-z_][A-Za-z0-9_]*)+$");
        }

        private readonly Dictionary<string, string>? recorded;
    }

    public static class GameApi
    {
        public static ApiNote Live()
        {
            var recorded = new Dictionary<string, string>
            {
                ["close"] = "Helpers.BusinessHelper.ShutdownBusiness",
                ["buy"] = "BizManPresentation.SendBuyBuildingOffer",
                ["price"] = "Buildings.Office.Headquarters.PricingManagerPlan.SetPrice",
                ["delivery"] = "UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination",
                ["clone"] = "BusinessLayoutSets.BusinessLayoutSetHelper.LoadBusinessLayoutSet",
                ["truck buy"] = "Buildings.ContractVehicleForSale.Order",
                ["slot assign"] = "Entities.VehicleSlot.AssignVehicle",
                ["hire"] = "Helpers.EmployeeHelper.HireCandidate",
                ["cashier"] = "Controllers.CashRegisterController.AssignEmployee",
                ["plan assign"] = "Buildings.Office.Headquarters.HrManagerPlan.AssignEmployee",
                ["purchasing plan"] = "UI.Smartphone.Apps.BizMan.PurchasingAgent.PurchasingAgentProductCellView.ChangeAssignedWarehouse",
                ["pricing plan create"] = "UI.Smartphone.Apps.BizMan.PricingManagers.PricingManagersPlanList.AddPlan",
                ["staff train"] = "Buildings.Office.Headquarters.HrManagerPlan.TrainEmployees",
                ["importer order"] = "UI.Smartphone.Apps.BizMan.PurchasingAgent.PurchasingAgentPlanUI.StartOrder",
                ["wholesaler contract drop"] = "Entities.DeliveryContract.Remove",
                ["wholesaler contract"] = "Dialogs.WholesaleStoreManagerDialog.StartContract",
                ["rent"] = "Buildings.BuildingCommands.Command_RentBuilding",
                ["cleaner"] = "CleaningStationController.AssignEmployee",
                ["guard"] = "Controllers.SecurityGuardLockerController.AssignEmployee",
                ["uniforms"] = "BuildingRegistration.uniformsBySkill",
                ["loan"] = "Dialogs.BankDialog.LoanRequest",
                ["loan repay"] = "LoanEntryUi.PayOffLoan",
                ["employee demand"] = "Entities.EmployeeInstance.GiveBonus",
                ["taxes pay"] = "Helpers.TaxHelper.PayCurrentTaxes",
                ["taxes back"] = "Helpers.TaxHelper.PayBackTaxes",
                ["time advance"] = "Timemachine.TimeMachine.SkipTime",
                ["sleep"] = "BedController.PerformActivity",
                ["food"] = "ItemHelper.TryToConsume",
            };
            return new ApiNote(
                LoadLog.PinnedBuild,
                recorded["close"],
                recorded["buy"],
                recorded["price"],
                recorded["delivery"],
                recorded["clone"],
                recorded);
        }

        public static ApiNote Read(IReadOnlyList<PanelLine>? panel, string? note)
        {
            if (panel == null || panel.Count == 0)
            {
                return new ApiNote("", "", "", "", "");
            }

            var build = "";
            var calls = new Dictionary<string, string>();
            foreach (var raw in (note ?? "").Split('\n'))
            {
                var line = raw.Trim().TrimEnd('\r');
                if (line.StartsWith("Build ", StringComparison.Ordinal))
                {
                    build = line.Substring("Build ".Length).Trim().TrimEnd('.');
                }

                if (!line.StartsWith("| ", StringComparison.Ordinal))
                {
                    continue;
                }

                var cells = line.Trim('|').Split('|');
                if (cells.Length != 2)
                {
                    continue;
                }

                var action = cells[0].Trim();
                var call = cells[1].Trim();
                if (action.Length == 0 || action == "action" || action.StartsWith("-", StringComparison.Ordinal))
                {
                    continue;
                }

                calls[action] = call;
            }

            return new ApiNote(
                build,
                Call(calls, "close"),
                Call(calls, "buy"),
                Call(calls, "price"),
                Call(calls, "delivery"),
                Call(calls, "clone"),
                calls);
        }

        private static string Call(Dictionary<string, string> calls, string action)
        {
            if (!calls.TryGetValue(action, out var call) || call == null)
            {
                return "";
            }

            return call;
        }
    }
}
