# Game API

Build 3682.

| action | call |
| --- | --- |
| close | Helpers.BusinessHelper.ShutdownBusiness |
| buy | BizManPresentation.SendBuyBuildingOffer |
| price | Buildings.Office.Headquarters.PricingManagerPlan.SetPrice |
| delivery | UI.Smartphone.Apps.BizMan.LogisticsManagers.LogisticsManagerPlanUI.AddDestination |
| clone | BusinessLayoutSets.BusinessLayoutSetHelper.LoadBusinessLayoutSet |

ShutdownBusiness clears the business. SendBuyBuildingOffer records the purchase. SetPrice writes retailPrices. AddDestination adds a destination on the current logistics plan and marks the save. LoadBusinessLayoutSet loads that layout into the building. SetBusinessLayout does not. ApplyInteriorDesign does not. Command_CloneItem copies the item in hand.
