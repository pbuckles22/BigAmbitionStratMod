# In-Store Capacity Scaling, Golden Layout Progression & Importer SKU Scope Lock Strategy (Exhaustive Engineering Spec)

## 1. Executive Summary: Two Mid-Game Scaling Bottlenecks Resolved

This specification closes the final two engineering gaps between Stage 1 bootstrap and Stage 4 compounding, and synchronizes in-store shelf capacity with the **$1.5\times$ delivery buffer math (`TargetStock = ceil(AvgDailyBurn * GapDays * 1.5)`)**:

1. **In-Store Capacity Scaling & "Golden Layout" Creation (Charlie Pryor Ep. 03):**
   * In `logistics_stocking_strategy_detailed.md`, each item's post-delivery shelf target is:
     $$\text{TargetStock}(s, i) = \left\lceil \text{AvgDailyBurn}(s, i) \times \text{GapDays} \times 1.5 \right\rceil$$
   * On a $2$-day delivery gap (`GapDays = 2`), `TargetStock` equals **$3.0 \times \text{AvgDailyBurn}$** ($2$ days of sales $\times 1.5$ buffer).
   * If a starter store only has $1$ display fixture (`FixtureCap = 100` to `150` units) and an item's average daily sales reach $40\text{–}50$ units/day, `TargetStock` hits $120\text{–}150$ units—saturating $100\%$ of the shelf capacity!
   * Furthermore, if Stage 1 only ever loads bare starter presets (`GiftShopWholesalers` on an `A1` $75\text{ m}^2$ lot with $1$ small register = $20$ customers/hr), then:
     1. High-traffic stores cap out at $20$ customers/hr (checkout bottleneck),
     2. No owned store ever has an upgraded layout (`Score > StarterScore`), so `Layout.Decide` never has a "Golden Layout" to clone, and
     3. When `Where.Decide` rents a `B1` or `C1` building size, cloning an `A1` layout fails because **blueprints in *Big Ambitions* are locked to exact building size codes (`BuildingSizeInfo`)**!

2. **The `durables` SKU Scope Lock vs. 1.0 Factories (Charlie Pryor Ep. 12):**
   * `pricing_profit_margin_neighborhood_competition_strategy.md` lists `ba:itemname_smartphone1` and `ba:itemname_earbuds01` under `durables` with Stage 2+ Importer margin floors ($45\%$).
   * **The 1.0 Engine Rule:** In *Big Ambitions 1.0*, high-tech Electronics (`smartphone1`, `earbuds01`, `headphones`, `smartwatch`) **cannot be imported from any overseas Importer**. They are restricted to tiny Wholesaler quantities or player-built **Factories** (which require multi-stage raw material and component assembly lines).
   * If `Open.Decide` opens an Electronics Store in Stage 2–4 expecting Purchasing Agents to order `smartphone1` from an Importer, `Restock.Order` will fail to find an Importer product, `Audit.Dry` will fire endlessly, and the store will starve!

---

## 2. In-Store Capacity Scaling & Checkout Expansion Engine (`StoreScale`)

### 2.1 The Three Store Bottlenecks & Their Exact Triggers

Every morning on `day.rolled` (once `SpendRoom >= $2,500`), evaluate each retail store $s$ for three physical upgrades:

| Upgrade Action | Bottleneck Predicate | Physical Mutation on `BuildingRegistration` | Capacity & Profit Impact |
| :--- | :--- | :--- | :--- |
| **1. Backroom Storage Rack / Display Shelf Addition (`add_storage_rack`)** | Any SKU $i$ in store $s$ has $\text{TargetStock}(s, i) = \lceil \text{AvgDailyBurn}(s, i) \times \text{GapDays} \times 1.5 \rceil \ge \text{FixtureCap}(s, i) \times 0.85$ | Add $1$ `ba:itemname_storagerack` (or second display fixture for SKU $i$) into available floor/backroom tiles (`FixtureCap += 200` units) | Raises `FixtureCap` and `ShelfHeadroom` by $+200$ units so `OrderAmount = TargetStock - OnHand` is never clipped by a small starter shelf! (Allowed in Stage 1+ whenever `SpendRoom >= $2,500`). |
| **2. Second Checkout Register + Cashier (`add_register`)** | `PeakHourlyCustomers(s) >= RegisterServiceCap(s) * 0.85` ($17$ cust/hr on a $20$/hr small register) AND `BuildingCustomerCapacity(s) > RegisterServiceCap(s)` | Add $1$ `ba:itemname_cashregister` + checkout counter (`RegisterServiceCap += 20` cust/hr) and hire/assign Cashier #2 (`8–16`) | Doubles store checkout throughput from $20 \rightarrow 40$ customers/hr ($160 \rightarrow 320$ customers/day). |
| **3. Extended Double-Shift Hours (`extend_hours_8_22`)** | `Stage >= 2` AND `CapacityUtilization(s) >= 0.80m` AND `RecentDailyProfit(s) >= $600` | Extend store open hours from `8–16` ($8\text{h}$) to **`8–22` ($14\text{h}$)** Mon–Sat; schedule Shift A (`8–15`) + Shift B (`15–22`) Cashiers & Cleaners | Increases daily revenue by $+65\%\text{–}+75\%$ on the exact same building rent (`RentPerDay` is fixed!). |

### 2.2 Building Size Code Matching (`BuildingSizeCode`) & Golden Layout Promotion

In Build 3682, `new BuildingSizeInfo(registration)` encodes the building's floorplan template (e.g., `A1`, `A2`, `B1`, `B2`, `C1`, `M1`).

* **Rule L1 (Strict Floorplan Code Keying):** `Layout.Decide(businessType, buildingSizeCode, ownedLayouts)` must match **both** `BusinessType` and `BuildingSizeCode` (`sizeKey = registration.BuildingCached.BuildingTypeCode`). Never attempt to clone an `A1` layout into a `B1` building.
* **Rule L2 (Golden Layout Auto-Save):** Whenever a store completes a capacity upgrade (`add_storage_rack` or `add_register`) and logs `ready=true` with a higher daily profit than any prior store of `(BusinessType, BuildingSizeCode)`, save/register its layout as the **Golden Layout** for `(BusinessType, BuildingSizeCode)` (`BusinessLayoutSetHelper.SaveBusinessLayoutSet`).

---

## 3. Explicit `durables` & Business Type Scope Lock (`SkuScope`)

To prevent `Open.Decide`, `Restock.Order`, and `Audit.Dry` from ever selecting factory-locked businesses or un-importable SKUs when Factories are out of scope:

### 3.1 Master Business Type & SKU Eligibility Matrix (Stages 1–4)

| Business Type (`ba:businesstype_*`) | `StoreFamily` | Stage 1 Eligible (Wholesaler) | Stage 2–4 Eligible (Importer / Zero-Truck) | Importer-Supplyable SKUs | Factory-Locked (Excluded Unless Factory Active) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **`giftshop`** | `specialty` | **YES** (`GiftShopWholesalers`) | **YES** | `cheapgift`, `expensivegift`, `umbrella`, `cheapflower`, `paperbag` | None |
| **`florist`** | `specialty` | **YES** (`FloristWholesalers`) | **YES** | `cheapflower`, `expensiveflower`, `cheapgift`, `paperbag` | None |
| **`fastfoodrestaurant`** | `food` | **YES** (`FastFoodKabob`) | **YES** | `kabob`, `burger`, `pizza`, `frenchfries`, `sodacan`, `paperbag` | None |
| **`coffeeshop`** | `food` | No | **YES** | `cupofcoffee`, `croissant`, `sodacan`, `paperbag` | None |
| **`bookstore`** | `specialty` | No | **YES** | `novel`, `technicalmanual`, `cheapgift`, `paperbag` | None |
| **`clothingstore`** | `durables` | No | **YES** | `classiccheapfemaleclothing`, `modernexpensivemaleclothing`, `paperbag` | None |
| **`jewelrystore`** | `durables` | No | **YES** | `cheapjewelry`, `expensivejewelry`, `paperbag` | None |
| **`furniturestore`** | `durables` | No | **YES** | `modernbed`, `modernsofa`, `moderntable`, `modernchair` | None |
| **`lawfirm` / `webdev` / `graphics` / `hair` / `gym`** | `office` / `service` | No | **YES** (Zero-Truck Pivot) | `haircareproduct` (Hairdresser only) | None |
| **`electronicsstore`** | `durables` | **NO** | **NO (Scope-Locked)** | *None for core phones/earbuds in 1.0* | **LOCKED:** `smartphone1`, `earbuds01`, `headphones`, `smartwatch` require 1.0 Player Factory! |

---

## 4. Complete C# Implementation (`src/ChaoticCEO/StoreScale.cs` & `SkuScope.cs`)

```csharp
using System;
using System.Collections.Generic;

namespace ChaoticCEO
{
    public sealed class StoreScalePick
    {
        public StoreScalePick(string storeAddress, string action, string itemOrStation, int addedCapacity, bool chosen)
        {
            StoreAddress = storeAddress ?? "";
            Action = action ?? "";
            ItemOrStation = itemOrStation ?? "";
            AddedCapacity = addedCapacity;
            Chosen = chosen;
        }

        public string StoreAddress { get; }

        public string Action { get; }

        public string ItemOrStation { get; }

        public int AddedCapacity { get; }

        public bool Chosen { get; }

        public static StoreScalePick None()
        {
            return new StoreScalePick("", "", "", 0, false);
        }
    }

    public static class StoreScale
    {
        public const int SmallRegisterHourlyCap = 20;

        public const int LargeCounterHourlyCap = 30;

        public const int StorageRackUnitCap = 200;

        public const decimal RegisterBusyThreshold = 0.85m;

        public const decimal RackTriggerRatio = 0.85m;

        public const decimal DoubleShiftProfitFloor = 600m;

        public static StoreScalePick DecideUpgrade(
            int stage,
            string? storeAddress,
            bool storeReady,
            decimal spendRoom,
            int registerCount,
            int buildingCustomerCap,
            int peakHourlyCustomers,
            int currentOpenHours,
            decimal recentDailyProfit,
            IReadOnlyDictionary<string, int>? avgBurnByItem,
            IReadOnlyDictionary<string, int>? fixtureCapByItem,
            int deliveryGapDays = Wholesaler.DefaultGapDays)
        {
            var addr = storeAddress ?? "";
            if (string.IsNullOrEmpty(addr) || spendRoom < 2_500m)
            {
                return StoreScalePick.None();
            }

            // 1. Check if any item's 1.5x delivery window TargetStock (Avg * GapDays * 1.5) exceeds 85% of its current fixture capacity.
            // Allowed in Stage 1+ so a fast-selling starter store never clips its 1.5x delivery buffer!
            if (avgBurnByItem != null)
            {
                foreach (var kv in avgBurnByItem)
                {
                    var item = kv.Key;
                    var burn = kv.Value;
                    if (string.IsNullOrEmpty(item) || burn <= 0)
                    {
                        continue;
                    }

                    var cap = Wholesaler.FixtureCapacity(item);
                    if (fixtureCapByItem != null && fixtureCapByItem.TryGetValue(item, out var liveCap) && liveCap > 0)
                    {
                        cap = liveCap;
                    }

                    var targetStock = Wholesaler.ComputeTargetStock(burn, deliveryGapDays);
                    if (targetStock >= cap * RackTriggerRatio)
                    {
                        return new StoreScalePick(addr, "add_storage_rack", item, StorageRackUnitCap, true);
                    }
                }
            }

            if (stage < 2 || !storeReady)
            {
                return StoreScalePick.None();
            }

            // 2. Check if checkout register throughput is bottlenecking building customer capacity.
            var regs = registerCount > 0 ? registerCount : 1;
            var checkoutCap = regs * SmallRegisterHourlyCap;
            if (buildingCustomerCap > checkoutCap
                && peakHourlyCustomers >= checkoutCap * RegisterBusyThreshold)
            {
                return new StoreScalePick(addr, "add_register", "ba:itemname_cashregister", SmallRegisterHourlyCap, true);
            }

            // 3. Check if store qualifies for 14-hour double-shift expansion (8-22).
            if (currentOpenHours <= 8
                && recentDailyProfit >= DoubleShiftProfitFloor
                && buildingCustomerCap > 0
                && peakHourlyCustomers >= buildingCustomerCap * 0.80m)
            {
                return new StoreScalePick(addr, "extend_hours_8_22", "schedule_8_22", 6, true);
            }

            return StoreScalePick.None();
        }
    }

    public static class SkuScope
    {
        private static readonly HashSet<string> FactoryLockedBusinesses = new HashSet<string>(StringComparer.Ordinal)
        {
            "ba:businesstype_electronicsstore",
        };

        private static readonly HashSet<string> FactoryLockedItems = new HashSet<string>(StringComparer.Ordinal)
        {
            "ba:itemname_smartphone1",
            "ba:itemname_earbuds01",
            "ba:itemname_headphones",
            "ba:itemname_smartwatch",
        };

        public static bool IsBusinessAllowed(string? businessType, bool factoryPipelineEnabled = false)
        {
            var type = businessType ?? "";
            if (string.IsNullOrEmpty(type))
            {
                return false;
            }

            if (!factoryPipelineEnabled && FactoryLockedBusinesses.Contains(type))
            {
                return false;
            }

            return true;
        }

        public static bool IsItemImportable(string? itemName, bool factoryPipelineEnabled = false)
        {
            var item = itemName ?? "";
            if (string.IsNullOrEmpty(item))
            {
                return false;
            }

            if (!factoryPipelineEnabled && FactoryLockedItems.Contains(item))
            {
                return false;
            }

            return true;
        }
    }
}
```

---

## 5. Copy-Pasteable Tier-1 xUnit Locks (`src/ChaoticCEO.Tests/StoreScaleAndScopeTests.cs`)

```csharp
using System.Collections.Generic;
using Xunit;

namespace ChaoticCEO.Tests
{
    public sealed class StoreScaleAndScopeTests
    {
        [Fact]
        public void StoreScale_adds_storage_rack_when_1_5x_delivery_window_target_reaches_eighty_five_percent_of_cap()
        {
            // Avg burn = 30/day on 2-day delivery gap -> TargetStock = 30 * 2 * 1.5 = 90 >= 85% of 100 fixture cap!
            var burn = new Dictionary<string, int> { ["ba:itemname_umbrella"] = 30 };
            var cap = new Dictionary<string, int> { ["ba:itemname_umbrella"] = 100 };

            var pick = StoreScale.DecideUpgrade(
                stage: 1,
                storeAddress: "3rd Street 35",
                storeReady: true,
                spendRoom: 5_000m,
                registerCount: 1,
                buildingCustomerCap: 30,
                peakHourlyCustomers: 12,
                currentOpenHours: 8,
                recentDailyProfit: 400m,
                avgBurnByItem: burn,
                fixtureCapByItem: cap,
                deliveryGapDays: 2);

            Assert.True(pick.Chosen);
            Assert.Equal("add_storage_rack", pick.Action);
            Assert.Equal("ba:itemname_umbrella", pick.ItemOrStation);
            Assert.Equal(200, pick.AddedCapacity);
        }

        [Fact]
        public void StoreScale_adds_second_register_when_checkout_hits_eighty_five_percent_and_building_has_headroom()
        {
            var burn = new Dictionary<string, int> { ["ba:itemname_cheapgift"] = 20 };
            var cap = new Dictionary<string, int> { ["ba:itemname_cheapgift"] = 150 };

            // 1 register = 20 cust/hr cap; building holds 35 cust/hr; peak is 18 cust/hr (>= 85% of 20).
            var pick = StoreScale.DecideUpgrade(
                stage: 2,
                storeAddress: "ba:street_seventhavenue 30",
                storeReady: true,
                spendRoom: 10_000m,
                registerCount: 1,
                buildingCustomerCap: 35,
                peakHourlyCustomers: 18,
                currentOpenHours: 8,
                recentDailyProfit: 500m,
                avgBurnByItem: burn,
                fixtureCapByItem: cap);

            Assert.True(pick.Chosen);
            Assert.Equal("add_register", pick.Action);
        }

        [Fact]
        public void SkuScope_blocks_factory_locked_electronics_stores_and_skus_when_factories_are_disabled()
        {
            Assert.False(SkuScope.IsBusinessAllowed("ba:businesstype_electronicsstore", factoryPipelineEnabled: false));
            Assert.True(SkuScope.IsBusinessAllowed("ba:businesstype_clothingstore", factoryPipelineEnabled: false));
            Assert.True(SkuScope.IsBusinessAllowed("ba:businesstype_jewelrystore", factoryPipelineEnabled: false));

            Assert.False(SkuScope.IsItemImportable("ba:itemname_smartphone1", factoryPipelineEnabled: false));
            Assert.False(SkuScope.IsItemImportable("ba:itemname_earbuds01", factoryPipelineEnabled: false));
            Assert.True(SkuScope.IsItemImportable("ba:itemname_cheapjewelry", factoryPipelineEnabled: false));
        }
    }
}
```