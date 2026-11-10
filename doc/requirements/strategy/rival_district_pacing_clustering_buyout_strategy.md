# Rival District Pacing, Street Clustering & Stage-4 Buyout Strategy (Exhaustive Engineering Spec)

## 1. Executive Conflict Diagnosis: `ClusterMin = 2` vs. Rival Aggression Thresholds

Comparing `overall_growth_strategy_stage_1_to_4.md` against `pricing_profit_margin_neighborhood_competition_strategy.md` reveals a direct strategic collision:
* **Site Selection (`Where.Decide`):** Prioritizes streets where `familyOnStreet >= ClusterMin` ($2$) and ranks remaining lots by `Traffic / DailyRent`. Because early saves often start in **Garment District** or **Lower Manhattan** where several cheap retail lots sit on the same street or inside the same neighborhood, `Where.Decide` will naturally place Store #1, Store #2, Store #3, and Store #4 in the **same neighborhood**.
* **AI Rival Aggression Triggers (Build 3682 Engine):**
  * **$\mathbf{1\text{–}3}$ Player Businesses in a Neighborhood (`SafeDistrictCap = 3`):** The local AI Rival (`Ingrid`, `kuo`, `Costas`, `David`) largely ignores the player unless directly undercut.
  * **$\mathbf{4\text{–}5}$ Player Businesses in a Neighborhood (`RivalPriceAttackThreshold = 4`):** The local AI Rival initiates **Price Attacks** on every overlapping SKU where the player sold $\ge 1$ unit yesterday, slashing rival prices down toward `WholesaleCost`.
  * **$\mathbf{6\text{–}7}$ Player Businesses in a Neighborhood (`RivalExpansionAttackThreshold = 6`):** The local AI Rival initiates **Expansion Attacks**, aggressively renting empty retail lots in that district to block player clustering.
  * **$\mathbf{8+}$ Player Businesses in a Neighborhood (`RivalPoachAttackThreshold = 8`):** The local AI Rival initiates **Employee Poaching Attacks**, sending recruitment offers to player staff who lack Gold Health Insurance or high satisfaction.

### Why Crossing $4$ Stores in a District Before Importer Supply Is Fatal
If the autopilot opens its $4\text{th}$ business inside a single neighborhood during Stage 1 or early Stage 2 *before* the single-family Warehouse + Importer pipeline is active for that store's family, the local Rival launches a **Price Attack** down to `WholesaleCost`. Because the player is still buying from Wholesalers at `WholesaleCost`, the player cannot undercut the rival (`UndercutTarget < Stage1MinViablePrice`), losing volume across all $4$ stores simultaneously!

---

## 2. The 4-Rule District Pacing & Clustering Policy (`RivalDistrictPolicy`)

### Rule R1 — Pre-Importer District Soft Cap (`PreImporterDistrictMax = 3`)
* While a store family is still supplied by **Wholesalers** (`stage == 1` or `!familyHasActiveImporterWarehouse`), `Where.Decide` enforces:
  $$\text{PlayerBusinessesInNeighborhood}(N) < \text{PreImporterDistrictMax}\ (3)$$
* **Effect:** Stores #1, #2, and #3 can still form a `ClusterMin = 2` or $3$-store cluster on the best street in District A (e.g., Garment District) without ever crossing the `RivalPriceAttackThreshold` ($4$)! If the bot opens a $4\text{th}$ store before Importer handover completes, `Where.Decide` automatically places it in District B (e.g., Hell's Kitchen or Midtown), starting a second 3-store cluster safely below the rival radar.

### Rule R2 — Post-Importer Contested District Unlock (`PostImporterDistrictMax = 7` Before Gold HR)
* Once Stage 2+ **Warehouse + Importer supply** is active for `targetFamily` (`familyImporterActive == true`), your `Stage2MinViablePrice` is $25\%\text{–}40\%$ below the rival's wholesale floor.
* You now *want* to win the district price war (`Mode C: Contested Neighborhood — Affordable Undercut`), so the district cap rises from $3$ to **`PreGoldHrDistrictMax = 7`**.
* Why cap at $7$ until HR Gold Health Insurance is active? Because opening the $8\text{th}$ business in a district triggers **Rival Employee Poaching (`RivalPoachAttackThreshold = 8`)**!

### Rule R3 — Uncapped District Dominance (`GoldHrActive == true`)
* Once `HrManagerPlan` has **Gold Health Insurance** (`GoldHrActive == true`) and employee satisfaction is $\ge 85\%$, the poaching shield is active and the district cap is completely removed (`int.MaxValue`).

### Rule R4 — The 20-Day Rival Attrition & Stage-4 Buyout Engine (`RivalBuyout.Decide`)
* In *Big Ambitions*, AI Rivals value their businesses based on a **20-day rolling profit average** (`RivalValuationWindowDays = 20`).
* When your Stage-2+ Importer-backed stores undercut a rival store in the same neighborhood for $\ge 20$ consecutive days, the rival store's rolling profit drops to $\le \$0/\text{day}$, collapsing its buyout valuation to its minimum floor (fixture/lease liquidation value).
* In **Stage 4**, `RivalBuyout.Decide` scans rival businesses in districts where we have sustained $\ge$ **`RivalAttritionDays = 20`** of undercutting and emits a buyout offer if:
  $$\text{BuyoutPrice} \le \text{SpendRoom}(\text{cash},\ \text{dailyCost}) \quad \land \quad \frac{\text{BuyoutPrice}}{\text{ProjectedConvertedDailyProfit}} \le \text{PaybackMax}\ (365\text{ days})$$

---

## 3. Master Decision Table: Neighborhood Selection & Rival Pacing

| Empire State | Player Businesses Currently in Neighborhood $N$ | Rival Aggression State in $N$ | `RivalDistrictPolicy.AllowOpenInNeighborhood` | Strategic Outcome |
| :--- | :--- | :--- | :--- | :--- |
| **Stage 1 / Wholesaler Supply (`!importerActive`)** | $0, 1, \text{ or } 2$ | Dormant ($< 4$) | **`true`** | Builds a $2\text{–}3$ store street cluster (`ClusterMin = 2`) in Neighborhood $N$ with zero rival price attacks. |
| **Stage 1 / Wholesaler Supply (`!importerActive`)** | $\ge 3$ | At threshold of Price Attack ($4$) | **`false` (Unless no other neighborhood has lots)** | Redirects Store #4 to a second neighborhood so no rival triggers a Price Attack while we are on Wholesaler costs. |
| **Stage 2+ Importer Active, `!goldHrActive`** | $0 \dots 6$ | Price/Expansion Attacks ($4\text{–}7$) | **`true`** | Importer costs ($25\%\text{–}40\%$ below wholesale) crush the rival's price attack while staying under the $8$-store poaching trigger. |
| **Stage 2+ Importer Active, `!goldHrActive`** | $\ge 7$ | At threshold of Poaching Attack ($8$) | **`false` (Until Gold HR is active)** | Prevents rival employee poaching until `HrPolicy` activates Gold Health Insurance for that district's staff. |
| **Stage 3–4 Importer Active + `goldHrActive`** | Any ($0 \dots \infty$) | Poaching Blocked by Gold HR | **`true`** | Full district saturation and 20-day rival profit attrition leading to Stage-4 rival buyouts. |

---

## 4. Complete C# Implementation (`src/ChaoticCEO/RivalDistrictPolicy.cs`)

```csharp
using System;
using System.Collections.Generic;

namespace ChaoticCEO
{
    public sealed class DistrictSiteCandidate
    {
        public DistrictSiteCandidate(
            string address,
            string street,
            string neighborhood,
            decimal traffic,
            decimal dailyRent,
            decimal cost,
            int familyOnStreet,
            int playerBusinessesInNeighborhood)
        {
            Address = address ?? "";
            Street = street ?? "";
            Neighborhood = neighborhood ?? "";
            Traffic = traffic;
            DailyRent = dailyRent;
            Cost = cost;
            FamilyOnStreet = familyOnStreet;
            PlayerBusinessesInNeighborhood = playerBusinessesInNeighborhood;
        }

        public string Address { get; }

        public string Street { get; }

        public string Neighborhood { get; }

        public decimal Traffic { get; }

        public decimal DailyRent { get; }

        public decimal Cost { get; }

        public int FamilyOnStreet { get; }

        public int PlayerBusinessesInNeighborhood { get; }
    }

    public sealed class RivalBuyoutCandidate
    {
        public RivalBuyoutCandidate(
            string businessId,
            string neighborhood,
            string businessType,
            decimal askingPrice,
            decimal rivalRollingDailyProfit,
            int daysUndercutByPlayer,
            decimal projectedPlayerDailyProfit)
        {
            BusinessId = businessId ?? "";
            Neighborhood = neighborhood ?? "";
            BusinessType = businessType ?? "";
            AskingPrice = askingPrice;
            RivalRollingDailyProfit = rivalRollingDailyProfit;
            DaysUndercutByPlayer = daysUndercutByPlayer;
            ProjectedPlayerDailyProfit = projectedPlayerDailyProfit;
        }

        public string BusinessId { get; }

        public string Neighborhood { get; }

        public string BusinessType { get; }

        public decimal AskingPrice { get; }

        public decimal RivalRollingDailyProfit { get; }

        public int DaysUndercutByPlayer { get; }

        public decimal ProjectedPlayerDailyProfit { get; }
    }

    public static class RivalDistrictPolicy
    {
        public const int PreImporterDistrictMax = 3;

        public const int PreGoldHrDistrictMax = 7;

        public const int RivalPriceAttackThreshold = 4;

        public const int RivalExpansionAttackThreshold = 6;

        public const int RivalPoachAttackThreshold = 8;

        public const int RivalAttritionDays = 20;

        public const int BuyoutPaybackMaxDays = 365;

        public static int MaxBusinessesInNeighborhood(bool importerActiveForFamily, bool goldHrActive)
        {
            if (!importerActiveForFamily)
            {
                return PreImporterDistrictMax;
            }

            if (!goldHrActive)
            {
                return PreGoldHrDistrictMax;
            }

            return int.MaxValue;
        }

        public static bool AllowInNeighborhood(
            int playerBusinessesInNeighborhood,
            bool importerActiveForFamily,
            bool goldHrActive)
        {
            var cap = MaxBusinessesInNeighborhood(importerActiveForFamily, goldHrActive);
            return playerBusinessesInNeighborhood < cap;
        }

        public static SitePick DecideSiteWithDistrictCap(
            decimal cash,
            decimal reserve,
            bool importerActiveForFamily,
            bool goldHrActive,
            IReadOnlyList<DistrictSiteCandidate>? candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return SitePick.None();
            }

            // First pass: filter to candidates that respect the active rival district cap.
            var capped = new List<SiteCandidate>();
            var all = new List<SiteCandidate>();
            foreach (var c in candidates)
            {
                if (c == null)
                {
                    continue;
                }

                var site = new SiteCandidate(
                    c.Address,
                    c.Street,
                    c.Traffic,
                    c.DailyRent,
                    c.Cost,
                    c.FamilyOnStreet);
                all.Add(site);

                if (AllowInNeighborhood(c.PlayerBusinessesInNeighborhood, importerActiveForFamily, goldHrActive))
                {
                    capped.Add(site);
                }
            }

            if (capped.Count > 0)
            {
                var pick = Where.Decide(cash, reserve, capped);
                if (pick.Chosen)
                {
                    return pick;
                }
            }

            // Fallback: if every neighborhood with compatible lots is at the cap, fall back to Where.Decide only if Importer is active.
            return importerActiveForFamily
                ? Where.Decide(cash, reserve, all)
                : SitePick.None();
        }

        public static RivalBuyoutCandidate? DecideBuyout(
            int stage,
            decimal spendRoom,
            IReadOnlyList<RivalBuyoutCandidate>? rivals)
        {
            if (stage < 4 || spendRoom <= 0m || rivals == null)
            {
                return null;
            }

            RivalBuyoutCandidate? best = null;
            var bestPayback = decimal.MaxValue;

            foreach (var r in rivals)
            {
                if (r == null
                    || r.AskingPrice <= 0m
                    || r.AskingPrice > spendRoom
                    || r.DaysUndercutByPlayer < RivalAttritionDays
                    || r.ProjectedPlayerDailyProfit <= 0m)
                {
                    continue;
                }

                var payback = r.AskingPrice / r.ProjectedPlayerDailyProfit;
                if (payback > BuyoutPaybackMaxDays)
                {
                    continue;
                }

                if (best == null || payback < bestPayback)
                {
                    best = r;
                    bestPayback = payback;
                }
            }

            return best;
        }
    }
}
```

---

## 5. Copy-Pasteable Tier-1 xUnit Locks (`src/ChaoticCEO.Tests/RivalDistrictPolicyTests.cs`)

```csharp
using Xunit;

namespace ChaoticCEO.Tests
{
    public sealed class RivalDistrictPolicyTests
    {
        [Fact]
        public void RivalDistrictPolicy_caps_neighborhood_at_three_stores_before_importer_to_prevent_price_attacks()
        {
            Assert.Equal(3, RivalDistrictPolicy.MaxBusinessesInNeighborhood(importerActiveForFamily: false, goldHrActive: false));
            Assert.Equal(7, RivalDistrictPolicy.MaxBusinessesInNeighborhood(importerActiveForFamily: true, goldHrActive: false));
            Assert.Equal(int.MaxValue, RivalDistrictPolicy.MaxBusinessesInNeighborhood(importerActiveForFamily: true, goldHrActive: true));

            var candidates = new[]
            {
                // Garment District already has 3 player stores (a 4th would trigger Rival Price Attack!)
                new DistrictSiteCandidate("Garment 40", "GarmentSt", "GarmentDistrict", traffic: 50m, dailyRent: 100m, cost: 2000m, familyOnStreet: 3, playerBusinessesInNeighborhood: 3),
                // Hell's Kitchen only has 1 player store
                new DistrictSiteCandidate("Hells 12", "TenthAve", "HellsKitchen", traffic: 45m, dailyRent: 100m, cost: 2000m, familyOnStreet: 1, playerBusinessesInNeighborhood: 1),
            };

            // Pre-Importer: skips Garment District (at 3 stores) and picks Hell's Kitchen!
            var preImporterPick = RivalDistrictPolicy.DecideSiteWithDistrictCap(
                cash: 20_000m,
                reserve: 5_000m,
                importerActiveForFamily: false,
                goldHrActive: false,
                candidates);
            Assert.True(preImporterPick.Chosen);
            Assert.Equal("Hells 12", preImporterPick.Address);

            // Post-Importer: Garment District cap rises to 7, so its 3-store cluster street wins!
            var postImporterPick = RivalDistrictPolicy.DecideSiteWithDistrictCap(
                cash: 20_000m,
                reserve: 5_000m,
                importerActiveForFamily: true,
                goldHrActive: false,
                candidates);
            Assert.True(postImporterPick.Chosen);
            Assert.Equal("Garment 40", postImporterPick.Address);
        }

        [Fact]
        public void RivalDistrictPolicy_buys_out_distressed_rival_after_twenty_days_of_undercutting_in_stage_4()
        {
            var rivals = new[]
            {
                new RivalBuyoutCandidate("rival_too_early", "HellsKitchen", "ba:businesstype_giftshop", askingPrice: 60_000m, rivalRollingDailyProfit: -50m, daysUndercutByPlayer: 12, projectedPlayerDailyProfit: 400m),
                new RivalBuyoutCandidate("rival_attrited", "HellsKitchen", "ba:businesstype_giftshop", askingPrice: 60_000m, rivalRollingDailyProfit: -120m, daysUndercutByPlayer: 21, projectedPlayerDailyProfit: 300m),
            };

            var pick = RivalDistrictPolicy.DecideBuyout(stage: 4, spendRoom: 100_000m, rivals);
            Assert.NotNull(pick);
            Assert.Equal("rival_attrited", pick!.BusinessId);
        }
    }
}
```