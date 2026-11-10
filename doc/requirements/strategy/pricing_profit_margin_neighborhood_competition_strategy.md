# Pricing, Profit Margin & Neighborhood Competition Strategy (Exhaustive Engineering Spec)

## 1. Executive Summary: The 4 Laws of Autonomous Pricing in *Big Ambitions*

1. **Competition Is Strictly Neighborhood-Scoped (`Neighborhood` $\times$ `ItemName`):**
   * In *Big Ambitions* (Build 3682), customer price elasticity and AI rival price wars operate **per neighborhood (district)**—Specifically: **Garment District**, **Hell's Kitchen**, **Midtown**, and **Lower Manhattan** (plus industrial/port zones).
   * A fast-food store selling `ba:itemname_pizza` or `ba:itemname_burger` in **Hell's Kitchen** competes *only* against other businesses selling that exact item in **Hell's Kitchen**.
   * If Hell's Kitchen has $3$ rival pizzerias pricing pizza at $\$11.00$, your Hell's Kitchen store must price near $\$10.75\text{–}\$11.00$ (if above your margin floor) to capture local demand. Meanwhile, if your store in **Garment District** faces zero local pizza rivals (or rivals pricing at $\$13.50$), your Garment District store must step its price up to $\$13.00\text{–}\$14.00$ to harvest maximum margin.
2. **Never Undercut Below Your Viable Margin Floor (`MinViablePrice`):**
   * AI Rivals are hardcoded in the game engine **never to price below wholesale cost** (`WholesaleCost`). They never take a negative gross margin on a unit sold; when rivals lose money, it is because thin gross margins fail to cover their fixed building rent and employee wages.
   * In **Stage 1**, your stores buy from the **exact same Wholesalers** (`NY Distro`, `Metro Wholesale`, etc.) as the AI rivals. If you undercut a rival down to wholesale cost in Stage 1, your gross margin approaches $0\%$ and your store bleeds `-$106/day` in fixed rent and wages.
   * In **Stage 2+**, switching to a **Warehouse + Purchasing Agent + Importer** cuts your unit cost $25\%\text{–}50\%$ **below** wholesale cost. That structural cost advantage allows your stores to undercut a rival's wholesale floor in contested neighborhoods while still earning a $45\%\text{–}65\%$ gross margin.
3. **When You Cannot Afford the Undercut Today, Hold the Floor & Compete on Non-Price Multipliers:**
   * If `NeighborhoodCompetitorPrice < MinViablePrice(unitCost, family, stage)`, **refuse to follow the rival down**. Clamp your price at `MinViablePrice` and win customer traffic via **Cleanliness ($100\%$)**, **Cashier Customer Service Skill ($80\%\text{–}100\%$)**, **Interior Score**, **Capacity**, and **Secondary Product Mix** until Stage 2 Importer supply lowers your `unitCost`.
4. **Never Put Stores from Different Neighborhoods on One Global `PricingManagerPlan`:**
   * A `PricingManagerPlan` in *Big Ambitions* applies one uniform price per item across every store assigned to that plan. Attaching a Hell's Kitchen store and a Garment District store to a single global `PricingManagerPlan` destroys neighborhood pricing. Price per store (`BuildingRegistration.retailPrices`) or group `PricingManagerPlan` instances strictly by `(Neighborhood, BusinessType)`.

---

## 2. Family-Specific Profit Margin Targets, Hard Floors & Unit Economics

Different business families have fundamentally different cost structures, customer ticket sizes, and wage-to-revenue ratios. The autopilot must enforce family-specific and stage-specific gross margin floors.

### 2.1 Mathematical Definitions

For any item $i$ at store $s$ with retail price $P > 0$ and unit acquisition cost $C > 0$ (where $C$ is the Wholesaler unit price in Stage 1, or the Importer landed unit cost in Stage 2+):

$$\text{GrossMargin}(P, C) = \frac{P - C}{P}, \qquad \text{Markup}(P, C) = \frac{P - C}{C}$$

Given a required minimum gross margin floor $m \in (0, 1)$, the minimum price from unit cost alone is:

$$\text{CostFloorPrice}(C, m) = \text{round}\!\left(\frac{C}{1 - m},\ 2\right)$$

To ensure the store also covers its fixed daily rent and wages (`DailyRent + DailyWages`), define the **Store Overhead Per Unit** over the store's expected or measured daily unit volume $U_{\text{store}} = \max(\text{MinStoreUnits},\ \text{MeasuredTotalDailyUnits}(s))$:

$$\text{OverheadPerUnit}(s) = \frac{\text{DailyRent}(s) + \text{DailyWages}(s)}{U_{\text{store}}}$$

The store's hard **Minimum Viable Price (`MinViablePrice`)** for item $i$ is the greater of the family margin floor and the overhead-covering break-even price (with a $10\%$ safety buffer, **OverheadCover** $= 1.10$):

$$\text{MinViablePrice}(s, i) = \max\!\Big(\text{CostFloorPrice}\big(C,\ \text{MinMargin}(F_i, \text{stage})\big),\ \text{round}\big(C + \text{OverheadPerUnit}(s) \times \text{OverheadCover},\ 2\big)\Big)$$

### 2.2 Master Family Margin & Markup Table

| Family (`StoreFamily`) | Business Types | Representative Items (`ba:itemname_*`) | Stage 1 Wholesaler **MinMarginFloor** | Stage 2+ Importer **MinMarginFloor** | **TargetMarginRange** (Monopoly / Healthy) | Target Price Multiplier ($P / C$) | Economic Rationale & Overhead Profile |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **`food`** | Fast Food, Coffee Shop, Fruit & Veg, Liquor Store | `pizza`, `burger`, `kabob`, `frenchfries`, `sodacan`, `cupofcoffee`, `croissant`, `apple`, `beer`, `whisky` | **$0.35$ ($35\%$)** | **$0.50$ ($50\%$)** | **$50\%\text{–}68\%$** | $1.54\times \text{ to } 3.12\times$ | High unit velocity ($150\text{–}600$ units/day), low dollar price ($\$1.50\text{–}\$15.00$), high staffing overhead (cashier + cooking/service stations + cleaner), and $1$ paper bag consumed per order. Needs $\ge 35\%$ in Stage 1 and $\ge 50\%$ in Stage 2+ to cover multi-worker payroll. |
| **`specialty`** | Gift Shop, Florist, Bookstore | `cheapgift`, `expensivegift`, `umbrella`, `cheapflower`, `expensiveflower`, `novel`, `technicalmanual` | **$0.40$ ($40\%$)** | **$0.55$ ($55\%$)** | **$58\%\text{–}75\%$** | $1.67\times \text{ to } 4.00\times$ | Medium unit velocity ($60\text{–}250$ units/day), medium ticket ($\$8.00\text{–}\$45.00$), and minimal payroll ($1$ cashier + $1$ cleaner in Stage 1). High customer price tolerance in Build 3682 makes this the #1 Stage-1 cash engine. |
| **`durables`** | Clothing Store, Jewelry Store, Electronics, Furniture, Car Dealership | `classiccheapfemaleclothing`, `modernexpensivemaleclothing`, `cheapjewelry`, `expensivejewelry`, `smartphone1`, `earbuds01` | **$0.30$ ($30\%$)** | **$0.45$ ($45\%$)** | **$50\%\text{–}70\%$** | $1.43\times \text{ to } 3.33\times$ | Lower unit velocity ($15\text{–}100$ units/day), high ticket ($\$45.00\text{–}\$2,500+$), large dollar contribution per unit (`P - C` is $\$25\text{–}\$800$), but higher building rent (`150m²–600m²`) and high inventory working capital. |
| **`service`** | Hairdresser, Gym, Nightclub, Travel Agency, Event Planning | `haircareproduct`, styling/cut fee, gym entry fee, club entry/drinks | **$0.40$ ($40\%$)** over hourly staff cost | **$0.50$ ($50\%$)** over hourly staff cost | **$55\%\text{–}75\%$** operating margin | $2.00\times \text{ to } 3.50\times$ hourly wage share | Near-zero physical COGS (except `haircareproduct`). Primary unit cost is the assigned employee's hourly wage divided by customers served per hour. |
| **`office`** | Law Firm, Web Dev Agency, Graphic Design, Recruitment Agency | Hourly billing rate per lawyer / web dev / designer / recruiter | **$0.35$ ($35\%$)** over hourly staff cost | **$0.50$ ($50\%$)** over hourly staff cost | **$60\%\text{–}80\%$** operating margin | $2.20\times \text{ to } 4.00\times$ hourly wage | Zero physical goods, zero trucks, zero paper bags. High professional wages ($\$40\text{–}\$120/\text{hr}$) require billing rates that clear $\ge 1.8\times\text{–}3.0\times$ hourly salary plus desk rent share. |

### 2.3 Item-Level Baseline Cost & Pricing Reference (Stage 1 Starter & Common Retail Items)

| Item Token (`ba:itemname_*`) | Family | Typical Default Retail Price | Typical Wholesaler Cost (Stage 1) | Typical Importer Cost (Stage 2+) | Stage 1 `MinViablePrice` ($m = 35\%\text{–}40\%$) | Stage 2+ `MinViablePrice` ($m = 50\%\text{–}55\%$) | Typical Monopoly Ceiling ($\ge 75\%$ Sat.) |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `ba:itemname_cheapgift` | `specialty` | $\$12.00$ | $\$5.20$ | $\$3.10$ | $\$8.67$ | $\$6.89$ | $\$16.50\text{–}\$19.00$ |
| `ba:itemname_expensivegift` | `specialty` | $\$28.00$ | $\$12.50$ | $\$7.50$ | $\$20.83$ | $\$16.67$ | $\$38.00\text{–}\$44.00$ |
| `ba:itemname_umbrella` | `specialty` | $\$15.00$ | $\$6.40$ | $\$3.80$ | $\$10.67$ | $\$8.44$ | $\$21.00\text{–}\$25.00$ |
| `ba:itemname_cheapflower` | `specialty` | $\$8.00$ | $\$3.40$ | $\$2.00$ | $\$5.67$ | $\$4.44$ | $\$11.50\text{–}\$13.50$ |
| `ba:itemname_expensiveflower` | `specialty` | $\$22.00$ | $\$9.50$ | $\$5.70$ | $\$15.83$ | $\$12.67$ | $\$30.00\text{–}\$35.00$ |
| `ba:itemname_sodacan` | `food` | $\$2.00$ | $\$0.85$ | $\$0.50$ | $\$1.31$ | $\$1.00$ | $\$2.75\text{–}\$3.40$ |
| `ba:itemname_burger` | `food` | $\$6.50$ | $\$2.80$ | $\$1.65$ | $\$4.31$ | $\$3.30$ | $\$9.00\text{–}\$11.00$ |
| `ba:itemname_pizza` | `food` | $\$10.00$ | $\$4.50$ | $\$2.60$ | $\$6.92$ | $\$5.20$ | $\$13.00\text{–}\$15.50$ |
| `ba:itemname_kabob` | `food` | $\$7.50$ | $\$3.20$ | $\$1.90$ | $\$4.92$ | $\$3.80$ | $\$10.50\text{–}\$12.50$ |
| `ba:itemname_frenchfries` | `food` | $\$3.50$ | $\$1.40$ | $\$0.80$ | $\$2.15$ | $\$1.60$ | $\$4.80\text{–}\$5.80$ |

---

## 3. Neighborhood-Scoped Competitor Discovery & Pricing State Machine

### 3.1 How Build 3682 Stores Neighborhoods & Competitor Prices in Memory

In `BigAmbitions.dll` (Build 3682), every `BuildingRegistration` in `SaveGameManager.Current.BuildingRegistrations` has:
1. **Neighborhood / District Identity:**
   * `registration.BuildingCached?.Neighborhood` (or `BuildingHelper.GetNeighborhood(registration.Address)` / street-to-neighborhood mapping from `registration.StreetName`).
   * If `BuildingCached` is null in headless tests, `StreetName` (or an explicit `neighborhood` string on the input record) groups stores by district/zone.
2. **Competitor Identification:**
   * A building is an active **competitor** in neighborhood $N$ if:
     * `!registration.RentedByPlayer && !registration.BuildingOwnedByPlayer` (or `!string.IsNullOrEmpty(registration.businessOwnerRivalId)`),
     * `!string.IsNullOrEmpty(registration.BusinessName)` and `!registration.temporarilyClosed`,
     * Its neighborhood equals $N$, AND
     * Its `registration.retailPrices` contains an entry for `itemName == item` with `price > 0f`.
3. **Neighborhood Competitor Benchmark Price (`NeighborhoodCompetitorPrice`):**
   * For a given `(neighborhood, item)`, collect all active competitor prices $P_{\text{comp}, 1}, \dots, P_{\text{comp}, k}$ in that exact neighborhood.
   * Define `NeighborhoodCompetitorPrice` as the **lowest active competitor price** in that neighborhood (`min(P_comp)`), or $0\text{m}$ if no competitor in that neighborhood sells `item`.

### 3.2 The 4 Neighborhood Pricing Modes (`Prices.Decide`)

| Mode | Condition on `(Store s, Item i, Neighborhood N)` | Pricing Action | Worked Example: Pizza in Hell's Kitchen vs. Garment District |
| :--- | :--- | :--- | :--- |
| **Mode A: Below Viable Floor Recovery** | `currentPrice < MinViablePrice(s, i)` | **Immediate Floor Lift:** Set `Price = max(MinViablePrice, currentPrice * (1 + PriceStep))` $\rightarrow$ `MinViablePrice`. | A store accidentally priced Pizza at $\$5.50$ while Stage-1 `MinViablePrice` is $\$6.92$: immediately raise to $\$6.92$. |
| **Mode B: Local Monopoly or High Capacity Utilization** | `NeighborhoodCompetitorPrice == 0m` **OR** `CapacityUtilization(s) >= 0.85m` **OR** (`currentUnits >= priorUnits` AND `satisfaction >= PriceSatisfied` ($0.75$)) | **Step Up Toward Satisfaction Ceiling:** Raise `Price = round(currentPrice * (1 + PriceStep), 2)` ($+5\%$/day) while `currentUnits >= priorUnits` and `satisfaction >= 0.75m`. | **Garment District:** No local rival sells Pizza (or local rival is at $\$13.50$ and our volume is holding at $\$12.50$ with $82\%$ satisfaction): step price up $+5\%$ to $\$13.13$. |
| **Mode C: Contested Neighborhood — Affordable Undercut** | `NeighborhoodCompetitorPrice > 0m` AND (`currentUnits < priorUnits` OR `satisfaction < PriceSatisfied` OR `currentPrice > NeighborhoodCompetitorPrice`) AND `UndercutTarget >= MinViablePrice(s, i)` | **Outprice Local Competitor:** Set `Price = UndercutTarget`, where `UndercutTarget = round(NeighborhoodCompetitorPrice * (1 - UndercutStep), 2)` (**UndercutStep** $= 0.02$, i.e., $2\%$ below local rival). | **Hell's Kitchen (Stage 2 Importer):** Local rival sells Pizza at $\$11.25$. Our Stage-2 `MinViablePrice` is $\$5.20$. Volume dipped at $\$12.00$. Bot sets Hell's Kitchen Pizza to $\$11.03$ (`11.25 * 0.98`), stealing the rival's customers at a $53\%$ margin. |
| **Mode D: Contested Neighborhood — Unaffordable Undercut ("Can't Afford It")** | `NeighborhoodCompetitorPrice > 0m` AND `UndercutTarget < MinViablePrice(s, i)` | **Refuse Suicide Undercut (Hold Floor / Best Trial):** Set `Price = max(MinViablePrice(s, i), BestTrialPriceAboveFloor)`. Trigger non-price multipliers (Section 4). | **Hell's Kitchen (Stage 1 Wholesaler):** Rival sells an item at $\$8.50$, but our Stage-1 Wholesaler `MinViablePrice` ($40\%$ margin + rent share) is $\$9.40$. Bot clamps at $\$9.40$ (never sells at a loss) and relies on $100\%$ cleanliness/service until Stage 2 Importer cuts cost. |

---

## 4. Six Non-Price Factors That Multiply Pricing Power & Beat Rivals

When Mode D triggers (or when maximizing the ceiling in Mode B), the game's customer attraction and price-satisfaction formulas depend on six operational levers beyond raw price:

### 4.1 Employee Customer Service Skill (`ba:skill_customerservice`)
* **Game Mechanic:** Each customer served evaluates `customerService` satisfaction based on the assigned cashier/server's skill level ($0\text{–}100\%$). Higher `customerService` directly buffers `pricing` satisfaction and raises the store's `overall` rating.
* **Bot Rule:** In Stage 2+, `HrManagerPlan.TrainEmployees` trains all cashiers and servers to $100\%$ skill, unlocking a $+10\%\text{–}+20\%$ higher retail price ceiling at the same $75\%$ satisfaction floor.

### 4.2 Store Cleanliness (`CleaningStationController` & Dirt Spots)
* **Game Mechanic:** Dirt spots accumulate based on foot traffic. Uncleaned dirt reduces `cleanliness` satisfaction, which drags down `overall` store rating and causes customers to walk out or complain about prices.
* **Bot Rule:** Every store assigns a dedicated Cleaner on Day 1 (`StoreOps.Assign` $\rightarrow$ `cleaner=Cleaner`) scheduled across open hours.

### 4.3 Interior Score & Fixture Tier (`Layout.Decide` & `CapacityBridge`)
* **Game Mechanic:** The `facility` score is determined by the interior layout score (`OwnedLayout.Score`) and matching `customerCapacity` (`BusinessHelper.UpdateCustomerCapacity`).
* **Bot Rule:** From Stage 2+, `Layout.Decide` clones the owned layout of that business type and building size with the highest interior score (`Layout.Lock`), maximizing `facility` satisfaction.

### 4.4 Store Capacity Utilization & Checkout Bottlenecks
* **Game Mechanic:** Every building has a hard customer capacity ceiling ($15, 30, 50, 75$ customers/hr), and each `CashRegisterController` has a hourly service limit ($20$/hr for small register, $30$/hr for large counter).
* **Bot Rule (`CapacityUtilization`):** Define `CapacityUtilization = HourlyCustomers / BuildingCustomerCapacity`. Whenever `CapacityUtilization >= 0.85m` ($85\%$), the store is physically near full capacity—**never cut prices to undercut a rival when `CapacityUtilization >= 0.85m`**, because the building cannot hold the extra customers anyway. Instead, step prices **up** by **PriceStep** ($5\%$) to convert excess traffic into pure margin.

### 4.5 Purchasing Agent Skill (`ba:skill_purchasingagent`) & Importer Cost Compression
* **Game Mechanic:** `MinViablePrice` is proportional to `unitCost`. Switching from Wholesaler to Importer in Stage 2 drops `unitCost` by $25\%\text{–}40\%$, and training the Purchasing Agent toward $100\%$ skill lowers Importer contract prices even further.
* **Bot Rule:** Never wage an aggressive undercut war in Stage 1 on Wholesaler costs. Wait until Stage 2+ when Importer costs drop `MinViablePrice` well below the rival's wholesale floor.

### 4.6 Rival Aggression Thresholds (`4` / `6` / `8` Businesses in a District)
* **Game Mechanic:** Each AI Rival controls a specific home district:
  * **1–3 player businesses in district:** Rivals mostly ignore you unless you directly undercut their primary product.
  * **4+ player businesses in district:** Rival triggers **Price Attacks** (cutting prices on overlapping items down toward their wholesale floor, provided you sold $\ge 1$ unit of that item yesterday).
  * **6+ player businesses in district:** Rival triggers **Expansion Attacks** (renting empty lots in your district).
  * **8+ player businesses in district:** Rival triggers **Poaching Attacks** (attempting to poach un-bonused employees).
* **Bot Rule:** Because rivals value their stores on a **20-day rolling profit window**, sustaining a Stage-2+ Importer-backed undercut (`UndercutTarget >= Stage2MinViablePrice`) for $20$ days crushes the rival store's profit without hurting your $50\%+$ margin.

---

## 5. Complete C# Implementation (`src/ChaoticCEO/Prices.cs`)

Below is the complete, drop-in replacement for `src/ChaoticCEO/Prices.cs` that preserves $100\%$ backward compatibility with existing callers and tests (`Prices.Lock` and the 8-argument `Prices.Decide`) while adding `MinMargin`, `MinViablePrice`, and the neighborhood-aware `Prices.Decide` overload:

```csharp
using System;
using System.Collections.Generic;

namespace ChaoticCEO
{
    public sealed class ItemSoldPerPriceEntry
    {
        public ItemSoldPerPriceEntry(string item, float price, int amount)
        {
            Item = item ?? "";
            Price = price;
            Amount = amount;
        }

        public string Item { get; }

        public float Price { get; }

        public int Amount { get; }
    }

    public sealed class SoldPrice
    {
        public SoldPrice(string item, float price)
        {
            Item = item ?? "";
            Price = price;
        }

        public string Item { get; }

        public float Price { get; }
    }

    public sealed class PricePick
    {
        public PricePick(string item, decimal price, bool chosen, string mode = "")
        {
            Item = item ?? "";
            Price = price;
            Chosen = chosen;
            Mode = mode ?? "";
        }

        public string Item { get; }

        public decimal Price { get; }

        public bool Chosen { get; }

        public string Mode { get; }

        public static PricePick None()
        {
            return new PricePick("", 0m, false, "");
        }
    }

    public static class Prices
    {
        public const decimal PriceStep = 0.05m;

        public const decimal PriceSatisfied = 0.75m;

        public const decimal UndercutStep = 0.02m;

        public const decimal OverheadCover = 1.10m;

        public const int MinStoreUnits = 30;

        public const decimal Stage1FoodMinMargin = 0.35m;

        public const decimal Stage1SpecialtyMinMargin = 0.40m;

        public const decimal Stage1DurablesMinMargin = 0.30m;

        public const decimal Stage1ServiceMinMargin = 0.40m;

        public const decimal Stage1OfficeMinMargin = 0.35m;

        public const decimal Stage2FoodMinMargin = 0.50m;

        public const decimal Stage2SpecialtyMinMargin = 0.55m;

        public const decimal Stage2DurablesMinMargin = 0.45m;

        public const decimal Stage2ServiceMinMargin = 0.50m;

        public const decimal Stage2OfficeMinMargin = 0.50m;

        public static decimal MinMargin(string? family, int stage)
        {
            var kind = family ?? "";
            var imported = stage >= 2;
            switch (kind)
            {
                case "food":
                    return imported ? Stage2FoodMinMargin : Stage1FoodMinMargin;
                case "specialty":
                    return imported ? Stage2SpecialtyMinMargin : Stage1SpecialtyMinMargin;
                case "durables":
                    return imported ? Stage2DurablesMinMargin : Stage1DurablesMinMargin;
                case "service":
                    return imported ? Stage2ServiceMinMargin : Stage1ServiceMinMargin;
                case "office":
                    return imported ? Stage2OfficeMinMargin : Stage1OfficeMinMargin;
                default:
                    return imported ? Stage2FoodMinMargin : Stage1FoodMinMargin;
            }
        }

        public static decimal MinViablePrice(
            decimal unitCost,
            string? family,
            int stage,
            decimal dailyOverhead = 0m,
            int dailyStoreUnits = MinStoreUnits)
        {
            if (unitCost <= 0m)
            {
                return 0m;
            }

            var margin = MinMargin(family, stage);
            var divisor = 1m - margin;
            var fromMargin = divisor <= 0m
                ? unitCost * 2m
                : decimal.Round(unitCost / divisor, 2, MidpointRounding.AwayFromZero);

            if (dailyOverhead <= 0m)
            {
                return fromMargin;
            }

            var units = dailyStoreUnits < MinStoreUnits ? MinStoreUnits : dailyStoreUnits;
            var overheadPerUnit = (dailyOverhead / units) * OverheadCover;
            var fromOverhead = decimal.Round(unitCost + overheadPerUnit, 2, MidpointRounding.AwayFromZero);
            return fromOverhead > fromMargin ? fromOverhead : fromMargin;
        }

        public static PricePick Decide(
            string? item,
            decimal currentPrice,
            decimal defaultPrice,
            decimal wholesale,
            int priorUnits,
            int currentUnits,
            decimal satisfaction,
            IReadOnlyList<ItemSoldPerPriceEntry>? trials)
        {
            var name = item ?? "";
            if (name.Length == 0 || currentPrice <= 0m)
            {
                return PricePick.None();
            }

            if (defaultPrice > 0m && currentPrice == defaultPrice)
            {
                return Raise(name, currentPrice, "default");
            }

            if (currentUnits >= priorUnits && satisfaction >= PriceSatisfied)
            {
                return Raise(name, currentPrice, "raise");
            }

            var stepped = StepBack(name, defaultPrice, wholesale, 0m, trials);
            if (stepped != null)
            {
                return stepped;
            }

            return new PricePick(name, currentPrice, true, "hold");
        }

        public static PricePick Decide(
            string? item,
            string? family,
            int stage,
            decimal currentPrice,
            decimal defaultPrice,
            decimal unitCost,
            decimal neighborhoodCompetitorPrice,
            int priorUnits,
            int currentUnits,
            decimal satisfaction,
            IReadOnlyList<ItemSoldPerPriceEntry>? trials,
            decimal dailyOverhead = 0m,
            int dailyStoreUnits = MinStoreUnits,
            decimal capacityUtilization = 0m)
        {
            var name = item ?? "";
            if (name.Length == 0 || currentPrice <= 0m)
            {
                return PricePick.None();
            }

            var resolvedFamily = !string.IsNullOrEmpty(family)
                ? family!
                : Advisor.CargoFamily(name) ?? "specialty";
            var minViable = MinViablePrice(unitCost, resolvedFamily, stage, dailyOverhead, dailyStoreUnits);

            // 1. Hard floor protection: never price below MinViablePrice.
            if (minViable > 0m && currentPrice < minViable)
            {
                return new PricePick(name, minViable, true, "floor");
            }

            // 2. Leave default price immediately.
            if (defaultPrice > 0m && currentPrice == defaultPrice)
            {
                var raisedDefault = currentPrice * (1m + PriceStep);
                var target = minViable > raisedDefault ? minViable : raisedDefault;
                return new PricePick(name, target, true, "default");
            }

            // 3. Near capacity (>= 85%) and satisfied -> always raise regardless of rivals.
            if (capacityUtilization >= 0.85m && satisfaction >= PriceSatisfied)
            {
                return Raise(name, currentPrice, "capacity");
            }

            // 4. Neighborhood competitor evaluation (strictly local to this store's district).
            if (neighborhoodCompetitorPrice > 0m)
            {
                var undercutTarget = decimal.Round(
                    neighborhoodCompetitorPrice * (1m - UndercutStep),
                    2,
                    MidpointRounding.AwayFromZero);

                var losingVolumeOrSat = currentUnits < priorUnits || satisfaction < PriceSatisfied;
                var pricedAboveRival = currentPrice > neighborhoodCompetitorPrice;

                if (losingVolumeOrSat || pricedAboveRival)
                {
                    // Mode D: Rival is cheaper than our viable margin floor -> refuse suicide undercut.
                    if (minViable > 0m && undercutTarget < minViable)
                    {
                        var trialFloor = StepBack(name, defaultPrice, unitCost, minViable, trials);
                        if (trialFloor != null && trialFloor.Price >= minViable)
                        {
                            return new PricePick(name, trialFloor.Price, true, "floor-trial");
                        }

                        return new PricePick(name, minViable, true, "floor-hold");
                    }

                    // Mode C: Undercut is affordable above MinViablePrice -> outprice local rival.
                    if (undercutTarget > 0m)
                    {
                        return new PricePick(name, undercutTarget, true, "undercut");
                    }
                }
            }

            // 5. Mode B: Local monopoly or holding volume & satisfaction -> step up +5%.
            if (currentUnits >= priorUnits && satisfaction >= PriceSatisfied)
            {
                return Raise(name, currentPrice, "raise");
            }

            // 6. Step back to best historical contribution price at or above MinViablePrice.
            var stepped = StepBack(name, defaultPrice, unitCost, minViable, trials);
            if (stepped != null)
            {
                return stepped;
            }

            var fallback = minViable > currentPrice ? minViable : currentPrice;
            return new PricePick(name, fallback, true, "hold");
        }

        public static SoldPrice Lock(string item, IReadOnlyList<ItemSoldPerPriceEntry> entries)
        {
            var name = item ?? "";
            ItemSoldPerPriceEntry? winner = null;
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    if (entry == null || entry.Item != name || entry.Amount <= 0)
                    {
                        continue;
                    }

                    if (winner == null || entry.Amount >= winner.Amount)
                    {
                        winner = entry;
                    }
                }
            }

            if (winner == null)
            {
                return new SoldPrice(name, 0f);
            }

            return new SoldPrice(winner.Item, winner.Price);
        }

        private static PricePick Raise(string item, decimal currentPrice, string mode)
        {
            return new PricePick(item, currentPrice * (1m + PriceStep), true, mode);
        }

        private static PricePick? StepBack(
            string item,
            decimal defaultPrice,
            decimal wholesale,
            decimal minViable,
            IReadOnlyList<ItemSoldPerPriceEntry>? trials)
        {
            if (trials == null)
            {
                return null;
            }

            ItemSoldPerPriceEntry? winner = null;
            var winnerGain = 0m;
            foreach (var entry in trials)
            {
                if (entry == null
                    || entry.Item != item
                    || entry.Amount <= 0
                    || entry.Price <= 0f)
                {
                    continue;
                }

                var price = (decimal)entry.Price;
                if (defaultPrice > 0m && price == defaultPrice)
                {
                    continue;
                }

                if (minViable > 0m && price < minViable)
                {
                    continue;
                }

                var gain = (price - wholesale) * entry.Amount;
                if (winner == null || gain >= winnerGain)
                {
                    winner = entry;
                    winnerGain = gain;
                }
            }

            return winner == null
                ? null
                : new PricePick(winner.Item, (decimal)winner.Price, true, "stepback");
        }
    }
}
```

---

## 6. Copy-Pasteable Tier-1 xUnit Tests (`src/ChaoticCEO.Tests/PriceDecideTests.cs`)

Add these unit and integration locks to `src/ChaoticCEO.Tests/PriceDecideTests.cs`:

```csharp
    [Fact]
    public void Prices_min_viable_price_locks_family_and_stage_margin_floors()
    {
        Assert.Equal(0.35m, Prices.MinMargin("food", stage: 1));
        Assert.Equal(0.50m, Prices.MinMargin("food", stage: 2));
        Assert.Equal(0.40m, Prices.MinMargin("specialty", stage: 1));
        Assert.Equal(0.55m, Prices.MinMargin("specialty", stage: 2));
        Assert.Equal(0.30m, Prices.MinMargin("durables", stage: 1));
        Assert.Equal(0.45m, Prices.MinMargin("durables", stage: 2));

        // Pizza: $4.50 wholesale in Stage 1 (35% floor) -> $4.50 / 0.65 = $6.92
        Assert.Equal(6.92m, Prices.MinViablePrice(4.50m, "food", stage: 1));
        // Pizza: $2.60 importer in Stage 2 (50% floor) -> $2.60 / 0.50 = $5.20
        Assert.Equal(5.20m, Prices.MinViablePrice(2.60m, "food", stage: 2));
        // Cheap gift: $5.20 wholesale in Stage 1 (40% floor) -> $5.20 / 0.60 = $8.67
        Assert.Equal(8.67m, Prices.MinViablePrice(5.20m, "specialty", stage: 1));
    }

    [Fact]
    public void Prices_decide_undercuts_same_neighborhood_competitor_while_garment_district_raises()
    {
        // Hell's Kitchen: local rival sells pizza at $11.25; Stage-2 importer cost is $2.60 (minViable $5.20).
        var hellsKitchen = Prices.Decide(
            "ba:itemname_pizza",
            family: "food",
            stage: 2,
            currentPrice: 12.00m,
            defaultPrice: 10.00m,
            unitCost: 2.60m,
            neighborhoodCompetitorPrice: 11.25m,
            priorUnits: 50,
            currentUnits: 40,
            satisfaction: 0.70m,
            trials: null);

        // Garment District: no local pizza rival (or rival is at $13.50 and volume holds at $12.50).
        var garmentDistrict = Prices.Decide(
            "ba:itemname_pizza",
            family: "food",
            stage: 2,
            currentPrice: 12.50m,
            defaultPrice: 10.00m,
            unitCost: 2.60m,
            neighborhoodCompetitorPrice: 0m,
            priorUnits: 50,
            currentUnits: 52,
            satisfaction: 0.82m,
            trials: null);

        Assert.True(hellsKitchen.Chosen);
        Assert.Equal(11.03m, hellsKitchen.Price);
        Assert.Equal("undercut", hellsKitchen.Mode);

        Assert.True(garmentDistrict.Chosen);
        Assert.Equal(13.125m, garmentDistrict.Price);
        Assert.Equal("raise", garmentDistrict.Mode);
    }

    [Fact]
    public void Prices_decide_never_undercuts_below_min_viable_margin_when_rival_is_cheaper()
    {
        // Stage 1 Wholesaler cost $5.20 + $120 daily overhead across 30 units -> MinViablePrice = $9.60.
        // Local rival prices at $8.50 (undercutTarget $8.33 < $9.60). Bot must hold $9.60 floor.
        var pick = Prices.Decide(
            "ba:itemname_cheapgift",
            family: "specialty",
            stage: 1,
            currentPrice: 10.50m,
            defaultPrice: 12.00m,
            unitCost: 5.20m,
            neighborhoodCompetitorPrice: 8.50m,
            priorUnits: 30,
            currentUnits: 20,
            satisfaction: 0.65m,
            trials: null,
            dailyOverhead: 120m,
            dailyStoreUnits: 30);

        Assert.True(pick.Chosen);
        Assert.Equal(9.60m, pick.Price);
        Assert.Equal("floor-hold", pick.Mode);
    }
```