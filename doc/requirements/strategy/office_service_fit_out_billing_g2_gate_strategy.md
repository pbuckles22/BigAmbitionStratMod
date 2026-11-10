# Office & Service Fit-Out, Billing, and `G2` Gate Exemption Strategy (Exhaustive Engineering Spec)

## 1. Executive Problem Statement: The Non-Retail `G2` Deadlock

In `overall_growth_strategy_stage_1_to_4.md` and `logistics_stocking_strategy_detailed.md`, `Open.Decide` pivots new business openings to **`service`** and **`office`** businesses (`ba:businesstype_lawfirm`, `ba:businesstype_webdevelopmentagency`, `ba:businesstype_graphicdesignagency`, `ba:businesstype_hairdresser`, `ba:businesstype_gym`) whenever filled warehouse trucks exceed **`TruckBusy`** ($0.85$).

However, in the current `StoreHealth.Assess` implementation:
1. Every player business is evaluated against the 5-gate retail checklist (`G1` Open, `G2` Stocked, `G3` Staffed, `G4` Selling, `G5` Profitable).
2. **`G2` (`Stocked`)** requires `FixtureCargoProof == true` (physical inventory boxes placed onto display shelves or product panels).
3. **The Deadlock:**
   * Pure **Offices** (`lawfirm`, `webdevelopmentagency`, `graphicdesignagency`, `recruitmentagency`) and **Pure Services** (`gym`) hold **zero physical cargo boxes** ($0$ SKUs). Their `FixtureCargo(building)` dictionary is permanently empty.
   * Consequently, `StoreHealth.Stocked` (`g2`) evaluates to `false` forever, `StoreHealth.Ready` remains `false` forever, `StoreHealthGate.ExpansionAllowed` locks to `false`, and `Stall.Decide` pauses the autopilot after **`StuckDays`** ($3$) with `autopilot.paused ... reason=stuck gate=g2`!
   * Furthermore, `CashierBridge.Live` searches for a `CashRegisterController`, which does not exist in an Office (offices use Specialist Desks + Computers) or a Hairdresser (which uses Styling Stations + Hair Wash Stations).

This specification resolves the deadlock by partitioning businesses into **3 Operational Modes (`BusinessMode`)**, defining exact `G1`–`G5` readiness predicates for each mode, specifying remote headless fit-out (`TryFitOut`), and locking hourly/service fee pricing formulas.

---

## 2. The 3 Operational Business Modes (`BusinessMode`)

Every player-rented or player-owned revenue building (excluding `warehouse` and `headquarters` cost centers) belongs to one of three operational modes:

| `BusinessMode` | Business Types (`ba:businesstype_*`) | `StoreFamily` | Physical Cargo Required (`G2`) | Primary Revenue Station (`G3`) | Supply Source | Truck Load Impact |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **`RetailGoods`** | `giftshop`, `florist`, `fastfoodrestaurant`, `coffeeshop`, `supermarket`, `clothingstore`, `jewelrystore`, `bookstore`, `liquorstore`, `furniturestore` | `specialty`, `food`, `durables` | **All layout sellable SKUs + `paperbag`** (`FixtureCargoProof == true`) | `CashRegisterController` / `CheckoutCounter` + `CleaningStation` | Wholesaler (Stage 1) $\rightarrow$ Single-Family Warehouse (Stage 2+) | $100\%$ (consumes truck boxes daily) |
| **`ConsumableService`** | `hairdresser`, `nightclub` | `service` | **Consumable SKU only** (`ba:itemname_haircareproduct` for Hairdresser; drinks for Nightclub) | Service Station (`StylingStation` / `BarCounter`) + `CleaningStation` | Wholesaler `DeliveryContract` (or `service`/`food` WH in Stage 3+) | Near-zero ($1$ SKU for Hairdresser) |
| **`PureWorkstation`** | `lawfirm`, `webdevelopmentagency`, `graphicdesignagency`, `recruitmentagency`, `gym`, `travelagency` | `office`, `service` | **Zero Cargo ($0$ SKUs).** `G2` verifies **Workstation Seat Proof** (`UsableWorkstations >= MinWorkstations`) | Specialist Workstation (`OfficeDesk` + `Computer` + `Chair`) or `ReceptionDesk` + `CleaningStation` | **None** ($0$ contracts, $0$ warehouse stops) | **$0\%$ (Zero Trucks Required)** |

---

## 3. Mode-Aware `G1`–`G5` Readiness Gates (`StoreHealth.Assess`)

### 3.1 Gate Definitions by `BusinessMode`

| Gate | `RetailGoods` Predicate | `ConsumableService` (`hairdresser`) Predicate | `PureWorkstation` (`law`, `web`, `graphics`, `gym`) Predicate |
| :--- | :--- | :--- | :--- |
| **`G1` (`Open`)** | `!temporarilyClosed && scheduledOpen` | `!temporarilyClosed && scheduledOpen` | `!temporarilyClosed && scheduledOpen` |
| **`G2` (`Stocked` / `Equipped`)** | `FixtureCargoProof`: every required retail SKU in layout has `OnHand > 0` | `ConsumableCargoProof`: `OnHand("ba:itemname_haircareproduct") > 0` AND `StylingStations >= 1` | **`WorkstationProof`:** `CompleteWorkstations >= MinWorkstations` ($4$ for small office, $1$ reception/equipment set for gym) |
| **`G3` (`Staffed`)** | `RegisterStaffed && CleanerStaffed && StaffSatisfaction >= 50f` | `ServiceStationStaffed && CleanerStaffed && StaffSatisfaction >= 50f` | `AssignedSpecialists >= CompleteWorkstations && CleanerStaffed && StaffSatisfaction >= 50f` |
| **`G4` (`Selling` / `Billing`)** | Completed open-day `dailyCustomers > 0 && dailyRevenue > 0m` | Completed open-day `dailyCustomers > 0 && dailyRevenue > 0m` | Completed open-day `dailyCustomers > 0 && dailyRevenue > 0m` (billable client hours $> 0$) |
| **`G5` (`Profitable`)** | Last **`ProfitProofDays`** ($3$) completed open days all have `Profit > 0m` | Last **`ProfitProofDays`** ($3$) completed open days all have `Profit > 0m` | Last **`ProfitProofDays`** ($3$) completed open days all have `Profit > 0m` |

### 3.2 Why `G2` Becomes `WorkstationProof` for Offices & Pure Services
In *Big Ambitions* (Build 3682), an office employee (`ba:skill_lawyer`, `ba:skill_webdeveloper`, `ba:skill_graphicdesigner`) cannot bill client hours unless their assigned desk group is complete (Desk + Office Chair + Desktop Computer/Monitor). By redefining `G2` for `PureWorkstation` buildings as:
$$\text{G2}_{\text{PureWorkstation}} \iff (\text{CompleteWorkstations} \ge \text{MinWorkstations}) \land (\text{CustomerCapacity} > 0)$$
we preserve the 5-gate architecture (`G1 && G2 && G3 && G4 && G5`) without ever checking cargo boxes on an office or gym!

---

## 4. Remote Headless Fit-Out Pipeline (`TryFitOut`) for Offices & Services

When `Open.Decide` picks an `office` or `service` business in Stage 3–4 (because `truckLoad > 0.85m` or because an office type has the highest daily profit), `LiveDump.TryFitOut` executes the 5-step remote state machine on `day.rolled`:

| Fit-Out Step | `RetailGoods` Action | `ConsumableService` (`hairdresser`) Action | `PureWorkstation` (`law`, `web`, `graphics`, `gym`) Action |
| :--- | :--- | :--- | :--- |
| **Step 1: `layout`** | Load `GiftShopWholesalers` / golden layout onto `registration` | Load `HairdresserStarter` / golden layout onto `registration` | Load `LawFirmStarter` / `WebDevStarter` / `GymStarter` layout onto `registration`; set hours `8–16` Mon–Sat (`Sunday` closed for offices) |
| **Step 2: `ops` (Cleaner)** | Hire & assign `ba:skill_cleaning` to `ba:itemname_cleaningstation` (`8–16`) | Hire & assign `ba:skill_cleaning` to `ba:itemname_cleaningstation` (`8–16`) | Hire & assign `ba:skill_cleaning` to `ba:itemname_cleaningstation` (`8–16`) |
| **Step 3: `capacity`** | Call `BusinessHelper.UpdateCustomerCapacity(registration)` | Call `BusinessHelper.UpdateCustomerCapacity(registration)` | Call `BusinessHelper.UpdateCustomerCapacity(registration)` (counts valid client/workstation slots) |
| **Step 4: `contract`** | Sign Wholesaler `DeliveryContract` (Stage 1) or connect WH (Stage 2+) | Sign Wholesaler `DeliveryContract` for `ba:itemname_haircareproduct` (`SeedBurn = 15`, `CoverOpenDays = 2` $\implies 30$ units) | **Auto-Pass (`ContractNeeded == false`):** Mark Step 4 complete immediately with $0$ contracts! |
| **Step 5: `staff` (`cashier` / `specialist`)** | Hire & assign Cashier (`ba:skill_customerservice`) to `CashRegister` | Hire & assign Hairdresser (`ba:skill_hairdresser`) to each Styling Chair (`WorkShiftType.Default`, `8–16`) | Hire & assign $1$ Specialist (`ba:skill_lawyer` / `ba:skill_webdeveloper` / `ba:skill_graphicdesigner`) per workstation desk (`8–16`), or Receptionist for Gym |

---

## 5. Office & Service Billing Rate & Pricing Mathematics

### 5.1 Unit Economics of Office & Service Billing
Unlike retail goods where unit cost $C$ is the box purchase price, the marginal unit cost of an **Office Specialist** or **Service Worker** is their **Hourly Wage Share plus Workstation Overhead**:

1. **Effective Hourly Cost Per Specialist (`SpecialistHourlyCost`):**
   Let $W_{\text{hr}}$ be the specialist's hourly wage (e.g., $\$55/\text{hr}$ for a Lawyer, $\$42/\text{hr}$ for a Web Developer, $\$22/\text{hr}$ for a Hairdresser), $S_{\text{hr}}$ be the number of customers/billable units served per hour by one specialist ($S_{\text{hr}} = 1.0$ for Office billable hours; $S_{\text{hr}} = 2.0$ for Haircuts), and $C_{\text{consumable}}$ be the consumable cost per service ($\$0$ for offices/gyms; $\approx \$2.50$ for `haircareproduct` per haircut):
   $$\text{UnitLaborCost} = \frac{W_{\text{hr}}}{S_{\text{hr}}} + C_{\text{consumable}}$$
2. **Building Overhead Share Per Billable Hour (`OverheadPerUnit`):**
   With $N_{\text{staff}}$ active specialists working $H_{\text{day}} = 8$ hours/day ($U_{\text{store}} = N_{\text{staff}} \times H_{\text{day}} \times S_{\text{hr}}$ daily billable units), and daily fixed cost $\text{DailyFixed} = \text{DailyRent} + \text{CleanerDailyWage}$:
   $$\text{OverheadPerUnit} = \frac{\text{DailyFixed}}{N_{\text{staff}} \times H_{\text{day}} \times S_{\text{hr}}}$$
3. **Minimum Viable Billing Rate (`MinViableBillingRate`):**
   Given the family margin floor $m = \text{Prices.MinMargin}(\text{family},\ \text{stage})$ ($0.35$ Stage 1 / $0.50$ Stage 2+ for `office`; $0.40$ Stage 1 / $0.50$ Stage 2+ for `service`):
   $$\text{MinViableBillingRate} = \text{round}\!\left(\frac{\text{UnitLaborCost} + \text{OverheadPerUnit}}{1 - m},\ 2\right)$$

### 5.2 Master Reference Table: Office & Service Billing Rates (Build 3682)

| Business Type | Skill Token | Typical Wage ($W_{\text{hr}}$) | Units/Hr ($S_{\text{hr}}$) | Consumable ($C_{\text{cons}}$) | Overhead / Unit ($4$ Desks) | Stage 2+ `MinViablePrice` ($m = 50\%$) | Target Ceiling ($\ge 75\%$ Sat.) |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Law Firm** (`lawfirm`) | `ba:skill_lawyer` | $\$60.00/\text{hr}$ | $1.0/\text{hr}$ | $\$0.00$ | $\$8.50$ | **$\$137.00/\text{hr}$** | $\$210.00\text{–}\$280.00/\text{hr}$ |
| **Web Dev Agency** (`webdevelopmentagency`) | `ba:skill_webdeveloper` | $\$45.00/\text{hr}$ | $1.0/\text{hr}$ | $\$0.00$ | $\$7.50$ | **$\$105.00/\text{hr}$** | $\$165.00\text{–}\$220.00/\text{hr}$ |
| **Graphic Design** (`graphicdesignagency`) | `ba:skill_graphicdesigner` | $\$36.00/\text{hr}$ | $1.0/\text{hr}$ | $\$0.00$ | $\$6.50$ | **$\$85.00/\text{hr}$** | $\$130.00\text{–}\$175.00/\text{hr}$ |
| **Hairdresser** (`hairdresser`) | `ba:skill_hairdresser` | $\$22.00/\text{hr}$ | $2.0/\text{hr}$ | $\$2.50$ | $\$3.50$ | **$\$34.00/\text{cut}$** | $\$55.00\text{–}\$75.00/\text{cut}$ |
| **Gym** (`gym`) | `ba:skill_customerservice` | $\$18.00/\text{hr}$ | $10.0/\text{hr}$ | $\$0.00$ | $\$2.20$ | **$\$8.00/\text{entry}$** | $\$14.00\text{–}\$20.00/\text{entry}$ |

---

## 6. Complete C# Implementation (`src/ChaoticCEO/OfficeService.cs` & `StoreHealth.cs` Extension)

```csharp
using System;
using System.Collections.Generic;

namespace ChaoticCEO
{
    public enum BusinessMode
    {
        RetailGoods = 0,
        ConsumableService = 1,
        PureWorkstation = 2,
    }

    public static class OfficeService
    {
        public const string HairCareItem = "ba:itemname_haircareproduct";

        public const int DefaultOfficeMinWorkstations = 4;

        public const int DefaultServiceMinWorkstations = 1;

        public static BusinessMode ModeFor(string? businessType)
        {
            var type = businessType ?? "";
            switch (type)
            {
                case "ba:businesstype_lawfirm":
                case "ba:businesstype_webdevelopmentagency":
                case "ba:businesstype_graphicdesignagency":
                case "ba:businesstype_recruitmentagency":
                case "ba:businesstype_gym":
                case "ba:businesstype_travelagency":
                    return BusinessMode.PureWorkstation;

                case "ba:businesstype_hairdresser":
                case "ba:businesstype_nightclub":
                    return BusinessMode.ConsumableService;

                default:
                    return BusinessMode.RetailGoods;
            }
        }

        public static string RequiredSkill(string? businessType)
        {
            var type = businessType ?? "";
            switch (type)
            {
                case "ba:businesstype_lawfirm":
                    return "ba:skill_lawyer";
                case "ba:businesstype_webdevelopmentagency":
                    return "ba:skill_webdeveloper";
                case "ba:businesstype_graphicdesignagency":
                    return "ba:skill_graphicdesigner";
                case "ba:businesstype_recruitmentagency":
                    return "ba:skill_recruiter";
                case "ba:businesstype_hairdresser":
                    return "ba:skill_hairdresser";
                default:
                    return "ba:skill_customerservice";
            }
        }

        public static bool NeedsDeliveryContract(string? businessType)
        {
            return ModeFor(businessType) != BusinessMode.PureWorkstation;
        }

        public static bool G2Ready(
            string? businessType,
            bool retailFixtureCargoProof,
            IReadOnlyDictionary<string, int>? cargoOnHand,
            int completeWorkstations,
            int minWorkstations = 0)
        {
            var mode = ModeFor(businessType);
            if (mode == BusinessMode.PureWorkstation)
            {
                var floor = minWorkstations > 0
                    ? minWorkstations
                    : (StoreFamily.For(businessType ?? "") == "office"
                        ? DefaultOfficeMinWorkstations
                        : DefaultServiceMinWorkstations);
                return completeWorkstations >= floor;
            }

            if (mode == BusinessMode.ConsumableService)
            {
                if (cargoOnHand == null)
                {
                    return false;
                }

                if ((businessType ?? "") == "ba:businesstype_hairdresser")
                {
                    return cargoOnHand.TryGetValue(HairCareItem, out var units)
                        && units > 0
                        && completeWorkstations >= DefaultServiceMinWorkstations;
                }

                return retailFixtureCargoProof;
            }

            return retailFixtureCargoProof;
        }

        public static decimal MinViableBillingRate(
            string? businessType,
            int stage,
            decimal specialistHourlyWage,
            decimal unitsPerSpecialistHour,
            decimal consumableCostPerUnit,
            decimal dailyFixedOverhead,
            int activeSpecialists,
            int openHoursPerDay = 8)
        {
            if (specialistHourlyWage <= 0m || unitsPerSpecialistHour <= 0m)
            {
                return 0m;
            }

            var family = StoreFamily.For(businessType ?? "");
            if (string.IsNullOrEmpty(family))
            {
                family = ModeFor(businessType) == BusinessMode.PureWorkstation ? "office" : "service";
            }

            var margin = Prices.MinMargin(family, stage);
            var unitLabor = (specialistHourlyWage / unitsPerSpecialistHour) + Math.Max(0m, consumableCostPerUnit);

            var staff = activeSpecialists > 0 ? activeSpecialists : 1;
            var hours = openHoursPerDay > 0 ? openHoursPerDay : 8;
            var dailyUnits = staff * hours * unitsPerSpecialistHour;
            var overheadPerUnit = dailyFixedOverhead > 0m && dailyUnits > 0m
                ? dailyFixedOverhead / dailyUnits
                : 0m;

            var totalUnitCost = unitLabor + overheadPerUnit;
            var divisor = 1m - margin;
            return divisor <= 0m
                ? decimal.Round(totalUnitCost * 2m, 2, MidpointRounding.AwayFromZero)
                : decimal.Round(totalUnitCost / divisor, 2, MidpointRounding.AwayFromZero);
        }
    }
}
```

---

## 7. Copy-Pasteable Tier-1 xUnit Locks (`src/ChaoticCEO.Tests/OfficeServiceTests.cs`)

```csharp
using System.Collections.Generic;
using Xunit;

namespace ChaoticCEO.Tests
{
    public sealed class OfficeServiceTests
    {
        [Fact]
        public void OfficeService_exempts_pure_workstation_businesses_from_cargo_g2_and_checks_desks()
        {
            Assert.Equal(BusinessMode.PureWorkstation, OfficeService.ModeFor("ba:businesstype_lawfirm"));
            Assert.Equal(BusinessMode.PureWorkstation, OfficeService.ModeFor("ba:businesstype_gym"));
            Assert.Equal(BusinessMode.ConsumableService, OfficeService.ModeFor("ba:businesstype_hairdresser"));
            Assert.Equal(BusinessMode.RetailGoods, OfficeService.ModeFor("ba:businesstype_giftshop"));

            Assert.False(OfficeService.NeedsDeliveryContract("ba:businesstype_lawfirm"));
            Assert.True(OfficeService.NeedsDeliveryContract("ba:businesstype_hairdresser"));

            // Law firm with 0 cargo boxes but 4 complete desks passes G2!
            var emptyCargo = new Dictionary<string, int>();
            Assert.True(OfficeService.G2Ready(
                "ba:businesstype_lawfirm",
                retailFixtureCargoProof: false,
                cargoOnHand: emptyCargo,
                completeWorkstations: 4));

            // Law firm with only 2 desks fails G2 (default minimum 4).
            Assert.False(OfficeService.G2Ready(
                "ba:businesstype_lawfirm",
                retailFixtureCargoProof: false,
                cargoOnHand: emptyCargo,
                completeWorkstations: 2));
        }

        [Fact]
        public void OfficeService_requires_haircare_product_and_station_for_hairdresser_g2()
        {
            var noStock = new Dictionary<string, int>();
            var stocked = new Dictionary<string, int> { [OfficeService.HairCareItem] = 25 };

            Assert.False(OfficeService.G2Ready("ba:businesstype_hairdresser", false, noStock, completeWorkstations: 2));
            Assert.True(OfficeService.G2Ready("ba:businesstype_hairdresser", false, stocked, completeWorkstations: 2));
        }

        [Fact]
        public void OfficeService_min_viable_billing_rate_covers_specialist_wage_and_overhead_at_stage_margin()
        {
            // Law firm in Stage 3: $60/hr lawyer, 1 billable hr/hr, $272/day rent+cleaner across 4 lawyers * 8 hrs = 32 billable hrs/day ($8.50/hr overhead).
            // Total unit cost = $68.50/hr. Stage 2+ office min margin = 50% -> MinViableBillingRate = $137.00/hr.
            var rate = OfficeService.MinViableBillingRate(
                "ba:businesstype_lawfirm",
                stage: 3,
                specialistHourlyWage: 60m,
                unitsPerSpecialistHour: 1m,
                consumableCostPerUnit: 0m,
                dailyFixedOverhead: 272m,
                activeSpecialists: 4,
                openHoursPerDay: 8);

            Assert.Equal(137.00m, rate);
        }
    }
}
```