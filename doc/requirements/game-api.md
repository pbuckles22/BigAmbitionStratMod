# Game API

Build 3682.

| action | call |
| --- | --- |
| close | Helpers.BusinessHelper.ShutdownBusiness |
| buy | BizManPresentation.SendBuyBuildingOffer |
| price | Buildings.Office.Headquarters.PricingManagerPlan.SetPrice |
| delivery | UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination |
| clone | BusinessLayoutSets.BusinessLayoutSetHelper.LoadBusinessLayoutSet |
| truck buy | Buildings.ContractVehicleForSale.Order |
| slot assign | Entities.VehicleSlot.AssignVehicle |
| hire | Helpers.EmployeeHelper.HireCandidate |
| plan assign | Buildings.Office.Headquarters.HrManagerPlan.AssignEmployee |
| purchasing plan | UI.Smartphone.Apps.BizMan.PurchasingAgent.PurchasingAgentProductCellView.ChangeAssignedWarehouse |
| pricing plan create | UI.Smartphone.Apps.BizMan.PricingManagers.PricingManagersPlanList.AddPlan |
| staff train | Buildings.Office.Headquarters.HrManagerPlan.TrainEmployees |
| importer order | UI.Smartphone.Apps.BizMan.PurchasingAgent.PurchasingAgentPlanUI.StartOrder |
| wholesaler contract drop | Entities.DeliveryContract.Remove |

ShutdownBusiness clears the business. SendBuyBuildingOffer records the purchase. SetPrice writes retailPrices. AddDestination on the plan screen appends an empty destination to the open plan. The redo button keeps that call. On a loaded save it moves an existing `LogisticsManagerPlanDestination` onto the plan whose warehouse business name is the new warehouse. A store that is only on a delivery contract is added to that plan. If that warehouse has no plan, the redo creates one: the target is that warehouse, and the headquarters is one that already owns a logistics plan, or the headquarters building. Food items of that family leave the wholesaler contract. Then `SaveGameManager.MarkChange`. It does not call the empty append. LoadBusinessLayoutSet loads that layout into the building. SetBusinessLayout does not. ApplyInteriorDesign does not. Command_CloneItem copies the item in hand. Order creates a vehicle delivery to a warehouse. Purchase pays and spawns the truck in the world. AssignVehicle writes the vehicle onto the slot. The warehouse driver station writes the driver id from its setup and has no method of its own. HireCandidate moves the candidate onto the save. AssignEmployee writes the HR plan's employee. ChangeLogisticsManager, ChangeHeadhunter, and PricingManagerPlan.AssignEmployee write the other plans. ChangeAssignedWarehouse sets the import product's warehouse. AddPlan on the pricing list constructs the plan and adds it. TrainEmployees raises skill toward the plan's training target. StartOrder marks the import partnership active. Remove drops the delivery contract. EndContract only asks. CancelContract clears the enabled flag.

## Unverified

The autopilot needs these and build 3682 has not been searched for them. Each is a discovery story (7.9 or Epic 13). A call moves to the table above only after its story records the class and method and a human check shows the change in game. Code does not call a name from this list.

| action | story | what it must do |
| --- | --- | --- |
| staff plan write | 7.9 | Write targets on an `HrManagerPlan` or a `HeadhunterPlan` |
| rent | 13.1 | Rent a building that is available for rent |
| starter layout | 13.2 | Load a layout on a fresh profile that has no saved layouts |
| opening hours | 13.2 | Set a business’s opening hours |
| store ops | 13.2 | Assign a cleaner, a guard, uniforms, and capacity fixtures |
| wholesaler contract | 13.3 | Create a recurring `DeliveryContract` from a wholesaler to a store |
| loan | 13.4 | Take and repay a bank loan |
| employee demand | 13.4 | Satisfy `jobdemand_*` on hired employees and managers |
| taxes pay | 13.4 | Pay taxes and back taxes |
| time advance | 13.5 | Skip time, with sleep and food for the player |
| marketing | 13.6 | Start and stop a marketing campaign |
| rival takeover decline | 13.6 | Decline a hostile rival buyout |
