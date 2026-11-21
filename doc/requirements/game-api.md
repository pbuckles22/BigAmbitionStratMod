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
| cashier | Controllers.CashRegisterController.AssignEmployee |
| plan assign | Buildings.Office.Headquarters.HrManagerPlan.AssignEmployee |
| purchasing plan | UI.Smartphone.Apps.BizMan.PurchasingAgent.PurchasingAgentProductCellView.ChangeAssignedWarehouse |
| pricing plan create | UI.Smartphone.Apps.BizMan.PricingManagers.PricingManagersPlanList.AddPlan |
| staff train | Buildings.Office.Headquarters.HrManagerPlan.TrainEmployees |
| importer order | UI.Smartphone.Apps.BizMan.PurchasingAgent.PurchasingAgentPlanUI.StartOrder |
| wholesaler contract drop | Entities.DeliveryContract.Remove |
| wholesaler contract | Dialogs.WholesaleStoreManagerDialog.StartContract |
| rent | Buildings.BuildingCommands.Command_RentBuilding |
| cleaner | CleaningStationController.AssignEmployee |
| guard | Controllers.SecurityGuardLockerController.AssignEmployee |
| uniforms | UniformLockerController.AssignUniforms |
| loan | Dialogs.BankDialog.LoanRequest |
| loan repay | LoanEntryUi.PayOffLoan |
| employee demand | Entities.EmployeeInstance.GiveBonus |
| taxes pay | Helpers.TaxHelper.PayCurrentTaxes |
| taxes back | Helpers.TaxHelper.PayBackTaxes |
| time advance | Timemachine.TimeMachine.SkipTime |
| sleep | BedController.PerformActivity |
| food | ItemHelper.TryToConsume |
| marketing start | Dialogs.MarketingAgencyDialog.OnMarketingSettingsSet |
| marketing stop | UI.Smartphone.Apps.BizMan.BizManMarketing.CancelAllCampaignsFromAgency |
| rival takeover decline | UI.Smartphone.Apps.BizMan.RealEstateSettings.CancelForSale |
| capacity fixtures | Helpers.BusinessHelper.UpdateCustomerCapacity |
| sell shelf cargo | ICargoHolder.RemoveFromCargo + GameManager.ChangeMoneySafe (`ba:transaction_cargoholderinventorysold`) |
| sell shelf placed | BuildingRegistration.RemoveItemInstanceFromBuilding + GameManager.ChangeMoneySafe (`ba:transaction_itemsold`) |
| pack shelf | ItemInstance.ConvertToCargoInstance + ICargoHolder.AddToCargo |
| course completed | EducationHelper.HasCompletedDiploma |
| course take | EducationHelper.Command_CompleteCourse |

ShutdownBusiness clears the business. SendBuyBuildingOffer records the purchase. SetPrice writes retailPrices. AddDestination on the plan screen appends an empty destination to the open plan. The redo button keeps that call. On a loaded save it moves an existing `LogisticsManagerPlanDestination` onto the plan whose warehouse business name is the new warehouse. A store that is only on a delivery contract is added to that plan. If that warehouse has no plan, the redo creates one: the target is that warehouse, and the headquarters is one that already owns a logistics plan, or the headquarters building. Food items of that family leave the wholesaler contract. Then `SaveGameManager.MarkChange`. It does not call the empty append. LoadBusinessLayoutSet loads that layout into the building. SetBusinessLayout does not. ApplyInteriorDesign does not. Command_CloneItem copies the item in hand. Order creates a vehicle delivery to a warehouse. Purchase pays and spawns the truck in the world. AssignVehicle writes the vehicle onto the slot. The warehouse driver station writes the driver id from its setup and has no method of its own. HireCandidate moves the candidate onto the save. AssignEmployee writes the HR plan's employee. ChangeLogisticsManager, ChangeHeadhunter, and PricingManagerPlan.AssignEmployee write the other plans. ChangeAssignedWarehouse sets the import product's warehouse. AddPlan on the pricing list constructs the plan and adds it. TrainEmployees raises skill toward the plan's training target. StartOrder marks the import partnership active. Remove drops the delivery contract. EndContract only asks. CancelContract clears the enabled flag. StartContract is the wholesale dialog entry that creates a recurring `DeliveryContract`. The mod writes that contract onto the save with the wholesale and store addresses. Command_RentBuilding rents the building at that street and number. AssignEmployee on a cleaning station puts the cleaner on that station. AssignEmployee on a security locker puts the guard on that locker. AssignUniforms writes the store uniform. AssignEmployee on a cash register puts the cashier on that register. An empty register is hired again. The reason is retired when that address is on `EmployeeHelper.RetiredEmployees`, quit when it is on `ResignedEmployeesAndLastRegistrationAssigned`, poached when the building lists a poached employee, and absent when none of those match. Retired wins when more than one is set. LoanRequest creates a `Loan`, adds it to the save, and pays out the cash. PayOffLoan reduces `remainingAmount` and removes the loan when it hits zero. GiveBonus clears an unmet employee demand complaint for that day. PayCurrentTaxes and PayBackTaxes pay the IRS balances. SkipTime advances the clock by a span such as `1d` or `7d`. PerformActivity on a player-owned bed starts sleep. TryToConsume eats a held food item and raises hunger (and energy when the item grants it). OnMarketingSettingsSet writes `marketingCampaigns` for the selected business and agency. CancelAllCampaignsFromAgency removes that agency’s campaigns. CancelForSale removes the building from `buildingsForSale`. UpdateCustomerCapacity recalculates `customerCapacity` from the fixtures the layout placed. HasCompletedDiploma reports whether that diploma is done. `DiplomaName.MarketDemands` is the Market Demands course BizMan requires before a second business. Command_CompleteCourse(diploma, true) marks that diploma completed and sets minutes studied to the required minutes. False clears the flag and the minutes. The morning calls it with true for the blocking diploma, then retries the fit-out step. The player does not walk to class.

## Unverified

The autopilot needs these and build 3682 has not been searched for them. Each is a discovery story (7.9 or Epic 13). A call moves to the table above only after its story records the class and method and a human check shows the change in game. Code does not call a name from this list.

| action | story | what it must do |
| --- | --- | --- |
| staff plan write | 7.9 | Write targets on an `HrManagerPlan` or a `HeadhunterPlan` |
