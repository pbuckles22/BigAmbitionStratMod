# Game API

Build 3682.

| action | call |
| --- | --- |
| close | Helpers.BusinessHelper.ShutdownBusiness |
| buy | BizManPresentation.SendBuyBuildingOffer |
| price | Buildings.Office.Headquarters.PricingManagerPlan.SetPrice |
| delivery | UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination |
| clone | BusinessLayoutSets.BusinessLayoutSetHelper.LoadBusinessLayoutSet |

ShutdownBusiness clears the business. SendBuyBuildingOffer records the purchase. SetPrice writes retailPrices. AddDestination on the plan screen appends an empty destination to the open plan. The redo button keeps that call. On a loaded save it moves an existing `LogisticsManagerPlanDestination` onto the plan whose warehouse business name is the new warehouse. A store that is only on a delivery contract is added to that plan. If that warehouse has no plan, the redo creates one: the target is that warehouse, and the headquarters is one that already owns a logistics plan, or the headquarters building. Food items of that family leave the wholesaler contract. Then `SaveGameManager.MarkChange`. It does not call the empty append. LoadBusinessLayoutSet loads that layout into the building. SetBusinessLayout does not. ApplyInteriorDesign does not. Command_CloneItem copies the item in hand.

## Unverified

The autopilot needs these and build 3682 has not been searched for them. Each is a discovery story (11.0 or Epic 13). A call moves to the table above only after its story records the class and method and a human check shows the change in game. Code does not call a name from this list.

| action | story | what it must do |
| --- | --- | --- |
| truck buy | 11.0 | Buy a freight truck and park it in a warehouse garage slot |
| slot assign | 11.0 | Put a vehicle and a delivery driver on a `VehicleSlot` |
| hire | 11.0 | Hire a candidate into a business or a headquarters plan |
| plan assign | 11.0 | Assign an employee to a logistics, HR, headhunter, or pricing plan |
| purchasing plan | 11.0 | Make a purchasing agent order a family for a warehouse |
| importer order | 11.0 | Place or repeat an importer order into a warehouse |
| rent | 13.1 | Rent a building that is available for rent |
| starter layout | 13.2 | Load a layout on a fresh profile that has no saved layouts |
| opening hours | 13.2 | Set a business’s opening hours |
| wholesaler contract | 13.3 | Create a recurring `DeliveryContract` from a wholesaler to a store |
| loan | 13.4 | Take and repay a bank loan |
| time advance | 13.5 | Skip time, with sleep and food for the player |
| marketing | 13.6 | Start and stop a marketing campaign |
