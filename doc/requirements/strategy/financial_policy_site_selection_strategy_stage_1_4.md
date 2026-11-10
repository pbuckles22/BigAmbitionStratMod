# Financial Policy & Site-Selection Strategy (Exhaustive Engineering Spec)

## 1. Forensic Root-Cause Diagnosis: Why `1.16.7.3` Stacked Loans & `1.16.7.5` Rented 15 Bare Lots

Tracing the live execution path across `LiveDump.cs`, `Money.cs`, `BankLoan.cs`, `BankLoanBridge.cs`, `Where.cs`, `Rent.cs`, and `WinRun.cs` exposes four interlocking bugs:

### 1.1 Bug #1 — Why `Where.Decide` Returned `None` (`where=0`) While `Rent.Choose` Succeeded on Day 1
* In `src/ChaoticCEO/Rent.cs` (line 48), the Day-1 rent selector filters lots with:
  ```csharp
  if (lot.DailyRent < 0m || lot.DailyRent > cash) continue;
  ```
  Notice that `Rent.Choose` **allows `DailyRent == 0m`**.
* In `src/ChaoticCEO/Where.cs` (line 52), the Stage-1+ expansion site selector filters candidates with:
  ```csharp
  if (site.DailyRent <= 0m || site.Cost > room) continue;
  ```
  Notice that `Where.Decide` **rejects `DailyRent <= 0m`** (because line 58 computes `var ratio = site.Traffic / site.DailyRent;`).
* In `LiveDump.OpenLots`, the code populates `DailyRent` from `ToMoney(registration.RentPerDay)`. In Build 3682, unrented buildings frequently report `registration.RentPerDay == 0f` until a lease is active (their asking rent lives on `registration.BuildingCached` or `BuildingHelper`), OR `LiveDump.SiteCandidates` passes `DailyRent == 0m` into `Where.Decide`, causing `Where.Decide` to reject **100% of available buildings**!

### 1.2 Bug #2 — Why `PendingOpen` Trapped `Playbook.Decide` in an Infinite Daily Loan Loop
* When Store #1 became `Ready`, `Open.Decide` picked `"ba:businesstype_giftshop"` and `WinRun.Day` set `WinRun.PendingOpen = "ba:businesstype_giftshop"`.
* On every subsequent day, `LiveDump.NextPlaybook` evaluated:
  ```csharp
  var expanding = WinRun.PendingOpen.Length > 0 || openChosen;
  ```
  Because `PendingOpen` stayed non-empty forever (kept by the comment *"Keep PendingOpen until a where step lands"*), `expanding` was permanently `true`, so `NextPlaybook` passed `plannedSpend: Money.Stage1StoreNeed` ($\$15,000$) into `Money.Decide` even when `sites.Count == 0` or `!where.Chosen`!
* Inside `WinRun.Next`, because `pending.Length > 0`, `open` was forced to `OpenPick.None()`, `where` returned `SitePick.None()`, and `Playbook.Decide` fell straight through to `money` (`loan 5000`) every single morning until cash crossed $\$15,000$—and whenever cash dipped or reserve was miscalculated, it borrowed again.

### 1.3 Bug #3 — Why `LiveDump.NextPlaybook` Ignored Existing Debt & Blocked Loan Repayment
* Look at `LiveDump.NextPlaybook` and `LiveDump.ExecuteMoney`:
  1. `NextPlaybook` passes `dailyCost: 0m` $\rightarrow$ `Money.Reserve(0m)` is $\$0$, so the bot holds **zero** 7-day operating reserve.
  2. `NextPlaybook` passes `loan: null` $\rightarrow$ `Money.Decide` is completely blind to `SaveGameManager.Current.Loans`! It never enters the `repay` branch and never subtracts existing daily loan payments (`loan.dailyPayment`) from `LoanCover` ($50\%$ of daily profit).
  3. `ExecuteMoney` has `if (detail.StartsWith("repay ")) return false;` $\rightarrow$ even if `Money.Decide` returned a `repay` step, `ExecuteMoney` refused to call `BankLoanBridge.Repay`!

### 1.4 Bug #4 — Why Fixing `where` Then Rented 15 Bare Lots With Only 1 Layout (`1.16.7.5` Soak)
* Once `Where.Decide` began returning valid addresses and `ExecutePlaybook` called `RentBridge.Live(rent)` for Store #2, `TryFitOut` ran on `day.rolled`.
* In `LiveDump.RunFitOutStep`, case `"layout"` checks:
  ```csharp
  if (!StarterBridge.Inside(rented)) return null;
  ```
  Because the player character was still standing inside Store #1, `StarterBridge.Inside(rented)` returned `false` for Store #2 on every single day roll!
* Worse, `OwnedStoreHealth(current)` skips any building where `string.IsNullOrEmpty(building.businessTypeName)`. Because Store #2 never loaded its layout, its `businessTypeName` remained `""`. Therefore, `OwnedStoreHealth` only returned `[Store #1 (Ready)]`, `StoreHealthGate.ExpansionAllowed` evaluated to `true`, and `Open` + `Where` rented Lot #3, Lot #4, ... Lot #15!

---

## 2. The 7-Rule Autonomous Financial Policy (Complete Mathematical Specification)

### Rule F1 — True 7-Day Operating Reserve (`ReserveDays = 7`)
* **Daily Empire Cost (`DailyCost`):** Sum of yesterday's `RentExpenses + SalaryExpenses + MarketingExpenses` across all player-owned buildings in `Days(current)`, plus `ExistingDailyLoanPayments` across `current.Loans`. If yesterday's statement is empty on Days 1–2, use a floor of **`MinStoreDailyCost`** $= \$120\text{m}$ per rented building.
* **Formulas:**
  $$\text{Reserve}(\text{dailyCost}) = \max(0,\ \text{dailyCost}) \times \text{ReserveDays}\ (7)$$
  $$\text{Available}(\text{cash},\ \text{dailyCost}) = \max\big(0,\ \text{cash} - \text{Reserve}(\text{dailyCost})\big)$$
  $$\text{SpendRoom}(\text{cash},\ \text{dailyCost}) = \text{Available}(\text{cash},\ \text{dailyCost}) \times \text{DaySpend}\ (0.25)$$

### Rule F2 — Actionable `PlannedSpend` Gate (Zero Borrowing When Expansion Is Blocked or Site-Less)
* **Predicate:** Define `ActionableExpansion` in Stage 1 as:
  $$\text{ActionableExpansion} \iff (\text{stage} = 1) \land \text{ExpansionAllowed}(\text{ownedHealth}) \land (\text{bareRentedLots} = 0) \land (\text{goodsStoreCount} < \text{Stage2Stores}) \land (\text{openChosen} \lor \text{PendingOpen} \neq \text{""}) \land (\text{HasValidSite}(\text{sites}) = \text{true})$$
* **Formula:**
  $$\text{PlannedSpend} = \begin{cases} \text{Stage1StoreNeed}\ (\$15,000) & \text{if } \text{ActionableExpansion} = \text{true} \\ 0 & \text{otherwise} \end{cases}$$
* **Effect:** If no compatible site exists (`sites.Count == 0`), or if Store #2 is still a bare lot waiting on fit-out (`bareRentedLots > 0`), or if all $3$ Stage-1 stores are already rented (`goodsStoreCount >= 3`), `PlannedSpend` is strictly $\$0$ and **zero loans can be taken**.

### Rule F3 — The 5-Day Organic Cash-Flow Patience Rule (`SelfFundDays = 5`)
* **Shortfall & Net Cash Flow:**
  $$\text{Shortfall} = \max\big(0,\ \text{PlannedSpend} - \text{Available}(\text{cash},\ \text{dailyCost})\big)$$
  $$\text{NetDailyCashFlow} = \text{ProjectedDailyProfit} - \text{ExistingDailyLoanPayments}$$
* **Patience Gate:** If $\text{NetDailyCashFlow} > 0$ AND
  $$\text{Shortfall} \le \text{NetDailyCashFlow} \times \text{SelfFundDays}\ (5)$$
  then `Money.Decide` returns `MoneyPick.None()`.
* **Rationale:** When organic store profits will fund the remaining shortfall within $5$ days, waiting avoids loan interest and keeps debt capacity dry.

### Rule F4 — Cumulative Debt-Service Coverage Ceiling (`LoanCover = 0.50m`)
* **Remaining Cover Formula:**
  $$\text{MaxTotalDailyPayment} = \text{ProjectedDailyProfit} \times \text{LoanCover}\ (0.50)$$
  $$\text{RemainingDailyCover} = \text{MaxTotalDailyPayment} - \text{ExistingDailyLoanPayments}$$
* **Gate:** Any candidate loan offer whose ` offer.DailyPayment > RemainingDailyCover` is rejected. If $\text{RemainingDailyCover} \le 0$, borrowing is completely locked out until profits rise or existing loans are paid down.

### Rule F5 — Shortfall-Matched Chunk Sizing (`$5,000` / `$1,000` Chunks)
* When Rules F2, F3, and F4 all approve borrowing:
  * Compute max cover-fit capacity $C_{\max}$ from `offers` where `offer.DailyPayment <= RemainingDailyCover`.
  * Base chunk size: `BankLoan.ChunkSize(C_max)` ($\$5,000$ when $C_{\max} \ge \$5,000$; $\$1,000$ when $C_{\max} < \$5,000$).
  * Cap chunk against `Shortfall`: if $\text{Shortfall} \le \text{SmallChunk}$ ($\$1,000$), borrow $\text{SmallChunk}$ ($\$1,000$) even when $C_{\max} > \$10,000$, so the bot never borrows $\$5,000$ to cover a $\$900$ gap.

### Rule F6 — Two-Trigger Loan De-Leveraging (Active Repayment)
* Whenever `loan != null && loan.Remaining > 0m`:
  1. **Trigger 6A (Rate Arbitrage — Any Stage):** If `loan.InterestRate > bestPaybackRate` and `Available(cash, dailyCost) >= loan.Remaining` (or `>= MinRepayStep`), emit `MoneyPick("repay", min(loan.Remaining, Available), 0, true)`.
  2. **Trigger 6B (Post-Expansion / Idle Surplus Sweep — Stages 1–4):** If `PlannedSpend <= 0m` (e.g., while a newly opened store is stabilizing for $3$ days, or while sitting at $3$ stores compounding toward $\$150\text{k}$ Stage 2) AND `Available(cash, dailyCost) > RepayFloor` ($\$5,000$):
     $$\text{Repayable} = \text{Available}(\text{cash},\ \text{dailyCost}) - \text{RepayFloor}\ (\$5,000)$$
     $$\text{RepayAmount} = \min(\text{loan.Remaining},\ \text{Repayable})$$
     If $\text{RepayAmount} \ge \text{MinRepayStep}$ ($\$1,000$) OR $\text{RepayAmount} == \text{loan.Remaining}$, emit `MoneyPick("repay", RepayAmount, 0, true)`.

### Rule F7 — Stage 2–4 Capital Allocation & `DaySpend` Ceiling
* In Stages 2–4, bootstrap borrowing is off (`stage != 1`).
* Every capital expenditure (Warehouse truck purchase, HQ setup, Stage-4 building offer) must fit inside `SpendRoom(cash, dailyCost)` ($25\%$ of cash above the 7-day reserve), with **delivering warehouse empty truck slots (`Trucks.Decide`)** having #1 spend priority after the reserve.

---

## 3. Multi-Day Numerical Walkthrough Table (Days 1 to 22)

| Day | Store State | Cash | `DailyCost` (`Reserve = 7d`) | `Available` (`Cash - Res`) | Gross Profit / Day | Loan Bal (`DailyPmt`) | `PlannedSpend` | `Shortfall` vs. `5d * NetProfit` | `Money.Decide` Action | Why |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **1** | Store #1 Rented & Fitted | $\$9,200$ | $\$120$ ($\$840$) | $\$8,360$ | $\$40$ (proj) | $\$500$ ($\$5$) | $\$0$ (`!Ready`) | N/A | `repay 500` (or `None` after Day-1 hook) | Store #1 not Ready; `PlannedSpend = 0`. |
| **2** | Store #1 Open Day 1 (`!Ready`) | $\$8,700$ | $\$120$ ($\$840$) | $\$7,860$ | $\$320$ | $\$500$ ($\$5$) | $\$0$ (`!Ready`) | N/A | **`repay 500`** | `Available ($7,860) - RepayFloor ($5,000) = $2,860 >= $500` full payoff! |
| **3–4** | Store #1 Open Days 2–3 (`!Ready`) | $\$9,100$ | $\$120$ ($\$840$) | $\$8,260$ | $\$450$ | $\$0$ ($\$0$) | $\$0$ (`!Ready`) | N/A | **`None`** | Debt is $\$0$; waiting for 3rd profitable open day (`ProfitProofDays = 3`). |
| **5** | Store #1 Hits `Ready` ($\times 3$ days); `open` $\rightarrow$ `where` | $\$9,600$ | $\$120$ ($\$840$) | $\$8,760$ | $\$480$ | $\$0$ ($\$0$) | $\$15,000$ | Shortfall $\$6,240 > 5 \times \$480$ ($\$2,400$) | **`loan 5000`** | Shortfall exceeds 5-day self-fund window; takes one $\$5,000$ chunk. |
| **6** | Store #2 Rented; `Available` now $\$13,760$ | $\$14,600$ | $\$160$ ($\$1,120$) | $\$13,480$ | $\$480$ | $\$5,000$ ($\$45$) | $\$0$ (Bare lot #2) | N/A (`bareLots == 1`) | **`None`** | Store #2 is a bare lot in fit-out (`PlannedSpend = 0`), and `where` already landed! |
| **7–10** | Store #2 Fits Out & Stabilizes (`!Ready`) | $\$12,200$ | $\$240$ ($\$1,680$) | $\$10,520$ | $\$850$ | $\$5,000$ ($\$45$) | $\$0$ (`!Ready`) | N/A | **`repay 5000`** | `Available ($10,520) - RepayFloor ($5,000) = $5,520 >= $5,000` $\rightarrow$ pays off Store #2 loan! |
| **11** | Stores #1 & #2 `Ready`; `Available = $13,200` | $\$14,880$ | $\$240$ ($\$1,680$) | $\$13,200$ | $\$900$ | $\$0$ ($\$0$) | $\$15,000$ | Shortfall $\$1,800 \le 5 \times \$900$ ($\$4,500$) | **`None` (Self-Fund)** | Shortfall ($\$1,800$) is covered in $2$ days of organic profit ($\le 5\text{d}$)! |
| **13** | Cash hits $\$16,680$; Store #3 Rented & Fitted | $\$16,680$ | $\$360$ ($\$2,520$) | $\$14,160$ | $\$1,350$ | $\$0$ ($\$0$) | $\$0$ (`3` stores cap) | N/A | **`None`** | $3$ stores reached (`Stage2Stores = 3`); compounds debt-free to $\$150\text{k}$! |

---

## 4. Site-Selection & Layout-Compatibility Engine (`Where.cs` & `LiveDump.SiteCandidates`)

To permanently fix `where=0` while preventing the bot from renting apartments, warehouses, or incompatible floorplans for retail stores:

### 4.1 Four Requirements for `LiveDump.SiteCandidates`
1. **Layout Compatibility Check (`StarterBridge.SupportsType`):**
   * For a Stage-1 retail opening of `targetType` (`WinRun.PendingOpen` or `"ba:businesstype_giftshop"`), find the matching `StarterOffer` in `Starter.StageOne()`.
   * Call `BusinessLayoutSetHelper.GetOrLoadBusinessLayoutSet(offer.BusinessType, new BuildingSizeInfo(registration), offer.Layout, false)`.
   * If this returns `null`, the building is an apartment, warehouse, office, or wrong size—**skip it**.
2. **Positive Daily Rent Resolution (`ResolveDailyRent`):**
   * Read `registration.RentPerDay`. If `<= 0f`, fallback to `registration.BuildingCached != null ? registration.BuildingCached.rent : 0f`, and if still `<= 0f`, fallback to `40m` (minimum small-lot rent) so `Where.Decide` never divides by zero or drops valid unrented buildings.
3. **Real Traffic Index (`ResolveTraffic`):**
   * Read `registration.TrafficIndex > 0 ? (decimal)registration.TrafficIndex : 20m`.
4. **Live Street Family Cluster Count (`familyOnStreet`):**
   * Count how many player-rented buildings on `registration.StreetName` have `StoreFamily.For(b.businessTypeName) == targetFamily`. Pass that integer as `familyOnStreet` so `Where.Decide` automatically prefers streets where **ClusterMin** $= 2$ is met.

---

## 5. Complete C# Implementation (`BankLoan.cs`, `Money.cs`, `LiveDump.cs` Excerpts)

### 5.1 Updated `src/ChaoticCEO/BankLoan.cs` (`NextChunk` With Existing Debt & Shortfall Cap)

```csharp
        public static LoanOffer? NextChunk(
            decimal projectedDailyProfit,
            IReadOnlyList<LoanOffer>? offers,
            int existingDailyPayment = 0,
            decimal shortfall = 0m)
        {
            if (offers == null || projectedDailyProfit <= 0m)
            {
                return null;
            }

            var cover = (projectedDailyProfit * LoanCover) - existingDailyPayment;
            if (cover <= 0m)
            {
                return null;
            }

            LoanOffer? capacity = null;
            foreach (var offer in offers)
            {
                if (offer == null
                    || offer.Amount < MinimumAmount
                    || offer.DailyPayment <= 0
                    || offer.DailyPayment > cover)
                {
                    continue;
                }

                if (capacity == null || offer.Amount > capacity.Amount)
                {
                    capacity = offer;
                }
            }

            if (capacity == null)
            {
                return null;
            }

            var take = ChunkSize(capacity.Amount);
            if (shortfall > 0m && shortfall <= SmallChunk && SmallChunk <= capacity.Amount)
            {
                take = SmallChunk;
            }

            if (take > capacity.Amount)
            {
                take = capacity.Amount;
            }

            if (take < MinimumAmount)
            {
                return null;
            }

            LoanOffer? exact = null;
            foreach (var offer in offers)
            {
                if (offer == null
                    || offer.Amount != take
                    || offer.DailyPayment <= 0
                    || offer.DailyPayment > cover)
                {
                    continue;
                }

                if (exact == null || offer.DailyPayment <= exact.DailyPayment)
                {
                    exact = offer;
                }
            }

            if (exact != null)
            {
                return exact;
            }

            var scaled = (int)System.Math.Ceiling(
                (double)(capacity.DailyPayment * (take / capacity.Amount)));
            if (scaled <= 0 || scaled > cover)
            {
                return null;
            }

            return new LoanOffer(take, scaled);
        }
```

### 5.2 Updated `src/ChaoticCEO/Money.cs`

```csharp
using System.Collections.Generic;

namespace ChaoticCEO
{
    public sealed class MoneyPick
    {
        public MoneyPick(string action, decimal amount, int dailyPayment, bool chosen)
        {
            Action = action ?? "";
            Amount = amount;
            DailyPayment = dailyPayment;
            Chosen = chosen;
        }

        public string Action { get; }

        public decimal Amount { get; }

        public int DailyPayment { get; }

        public bool Chosen { get; }

        public static MoneyPick None()
        {
            return new MoneyPick("", 0m, 0, false);
        }
    }

    public static class Money
    {
        public const int ReserveDays = 7;

        public const decimal DaySpend = 0.25m;

        public const decimal LoanCover = BankLoan.LoanCover;

        public const decimal Stage1StoreNeed = 15_000m;

        public const int SelfFundDays = 5;

        public const decimal RepayFloor = 5_000m;

        public const decimal MinRepayStep = 1_000m;

        public static decimal Reserve(decimal dailyCost)
        {
            if (dailyCost <= 0m)
            {
                return 0m;
            }

            return dailyCost * ReserveDays;
        }

        public static decimal SpendRoom(decimal cash, decimal dailyCost)
        {
            var above = cash - Reserve(dailyCost);
            if (above <= 0m)
            {
                return 0m;
            }

            return above * DaySpend;
        }

        public static decimal Available(decimal cash, decimal dailyCost)
        {
            var above = cash - Reserve(dailyCost);
            return above > 0m ? above : 0m;
        }

        public static MoneyPick Decide(
            int stage,
            decimal cash,
            decimal dailyCost,
            decimal projectedDailyProfit,
            IReadOnlyList<LoanOffer>? offers,
            LoanBalance? loan,
            decimal bestPaybackRate,
            decimal plannedSpend = 0m,
            int existingDailyLoanPayment = 0)
        {
            var available = Available(cash, dailyCost);

            if (loan != null
                && loan.Remaining > 0m
                && loan.InterestRate > bestPaybackRate)
            {
                return new MoneyPick("repay", loan.Remaining, 0, true);
            }

            if (loan != null
                && loan.Remaining > 0m
                && plannedSpend <= 0m
                && available > RepayFloor)
            {
                var repayable = available - RepayFloor;
                var amount = repayable >= loan.Remaining ? loan.Remaining : repayable;
                if (amount >= MinRepayStep || amount == loan.Remaining)
                {
                    return new MoneyPick("repay", amount, 0, true);
                }
            }

            if (stage != 1 || offers == null || projectedDailyProfit <= 0m)
            {
                return MoneyPick.None();
            }

            if (plannedSpend <= 0m || available >= plannedSpend)
            {
                return MoneyPick.None();
            }

            var shortfall = plannedSpend - available;
            var netDailyProfit = projectedDailyProfit - existingDailyLoanPayment;
            if (netDailyProfit > 0m && shortfall <= netDailyProfit * SelfFundDays)
            {
                return MoneyPick.None();
            }

            var chunk = BankLoan.NextChunk(
                projectedDailyProfit,
                offers,
                existingDailyLoanPayment,
                shortfall);
            if (chunk == null)
            {
                return MoneyPick.None();
            }

            return new MoneyPick("loan", chunk.Amount, chunk.DailyPayment, true);
        }
    }
}
```

---

## 6. Copy-Pasteable Tier-1 xUnit Locks (`src/ChaoticCEO.Tests/MoneyDecideTests.cs`)

```csharp
    [Fact]
    public void Money_decide_waits_on_organic_profits_when_shortfall_fits_inside_self_fund_days()
    {
        Assert.Equal(5, Money.SelfFundDays);
        // Cash $13,800, DailyCost $120 -> Reserve $840, Available $12,960.
        // PlannedSpend $15,000 -> Shortfall $2,040.
        // Projected profit $500/day -> 5 * $500 = $2,500 >= $2,040 -> wait on profits!
        var wait = Money.Decide(
            1,
            cash: 13_800m,
            dailyCost: 120m,
            projectedDailyProfit: 500m,
            offers: new[] { new LoanOffer(5_000m, 50) },
            loan: null,
            bestPaybackRate: 0.1m,
            plannedSpend: Money.Stage1StoreNeed);

        Assert.False(wait.Chosen);
        Assert.Equal("", wait.Action);
    }

    [Fact]
    public void Money_decide_repays_active_loan_from_surplus_above_repay_floor_when_not_expanding()
    {
        Assert.Equal(5_000m, Money.RepayFloor);
        var repay = Money.Decide(
            1,
            cash: 11_840m,
            dailyCost: 120m,
            projectedDailyProfit: 400m,
            offers: new[] { new LoanOffer(5_000m, 50) },
            loan: new LoanBalance(remaining: 5_000m, dailyInterest: 15, interestRate: 0.05m),
            bestPaybackRate: 0.10m,
            plannedSpend: 0m);

        Assert.True(repay.Chosen);
        Assert.Equal("repay", repay.Action);
        Assert.Equal(5_000m, repay.Amount);
    }

    [Fact]
    public void Money_decide_subtracts_existing_daily_loan_payment_from_loan_cover()
    {
        // Profit $200 -> 50% cover = $100/day. Existing loan pays $70/day -> only $30/day cover left.
        var blocked = Money.Decide(
            1,
            cash: 2_000m,
            dailyCost: 100m,
            projectedDailyProfit: 200m,
            offers: new[] { new LoanOffer(5_000m, 45) },
            loan: null,
            bestPaybackRate: 0.1m,
            plannedSpend: Money.Stage1StoreNeed,
            existingDailyLoanPayment: 70);

        Assert.False(blocked.Chosen);
    }
```