# 1.0 Tax Deduction, Accrual Reserve & CapEx Timing Strategy (Exhaustive Engineering Spec)

## 1. Executive Summary: Why *Big Ambitions 1.0* Taxes Break Unbuffered Reserve Math

In *Big Ambitions 1.0* (Build 3682, highlighted in Charlie Pryor Ep. 05), the corporate/personal tax system enforces three mechanics that directly impact autonomous cash management:
1. **10% Late-Payment Penalty (`LateTaxPenaltyRate = 0.10m`):** If an assessed tax bill is not paid by its due date, the unpaid balance is hit with a $10\%$ penalty and converts into back taxes (`PayBackTaxes`).
2. **Partial Tax Payments Allowed:** Unlike pre-1.0 versions where tax bills were all-or-nothing, 1.0 allows partial payments against both current and back taxes (`PayCurrentTaxes(amount)` / `PayBackTaxes(amount)`), reducing the principal that would otherwise incur the $10\%$ penalty.
3. **100% Growth & CapEx Tax Deductions:** Capital expenditures spent on growing the empire—specifically **Freight Trucks**, **Warehouse Pallet Shelves**, **Store Fixtures/Upgrades**, and **Real Estate / Building Purchases**—reduce taxable profit in the period they are incurred.

### The Two Bugs in Current `Money.cs` & `Audit.cs`
* **Bug #1 (Unbuffered Tax Cliff):** `Money.Reserve(dailyCost)` only holds `7 * dailyCost` (rent + wages + marketing + loan payments). It holds **$\$0$ for accrued taxes**. When a Stage-3 or Stage-4 empire generating $\$40,000/\text{day}$ in profit receives a periodic tax assessment of $\$180,000+$, `Available(cash, dailyCost)` treats that $\$180,000$ as free `SpendRoom` right up until tax day—causing either a reserve breach (`cashBelowReserve = 1`) or a $10\%$ late penalty!
* **Bug #2 (Missed Tax Shield Before Assessment):** Spending cash on trucks, warehouse shelves, or Stage-4 real estate *after* the tax period closes forfeits the deduction for that cycle, whereas accelerating planned CapEx inside the **Pre-Tax Deduction Window (`TaxShieldWindowDays = 5`)** shields taxable income at the marginal tax rate ($20\%\text{–}35\%$).

---

## 2. Mathematical Model of Tax Accrual, Effective Reserve & Partial Payments

### 2.1 Daily Accrued Tax Liability (`AccruedTax`)
Let $\text{CycleNetProfit}$ be the cumulative net profit earned since the last tax assessment day across all player businesses (from `Days(current)` financial statements), and let $\text{DeductibleCapEx}$ be the qualifying growth expenditure (new store furnishings, warehouse pallet shelves, trucks, and building purchases) during the same tax cycle:

$$\text{TaxableCycleIncome} = \max\big(0,\ \text{CycleNetProfit} - \text{DeductibleCapEx}\big)$$

Using the effective progressive tax rate $\tau_{\text{eff}} \in [0.15,\ 0.35]$ (defaulting to **`EstimatedTaxRate = 0.25m`** when exact bracket tables are not read from save state), the **Unbilled Accrued Tax Liability** is:

$$\text{UnbilledAccruedTax} = \text{round}\big(\text{TaxableCycleIncome} \times \text{EstimatedTaxRate},\ 2\big)$$

Let $\text{BilledTaxDue}$ be the active unpaid tax bill currently shown in `TaxBillBridge.Due()` (current taxes + back taxes). The empire's **Total Tax Reserve Requirement (`TaxReserve`)** is:

$$\text{TaxReserve} = \text{BilledTaxDue} + \text{UnbilledAccruedTax}$$

### 2.2 Tax-Aware Operating Reserve (`Money.ReserveWithTax`)
Extend the 7-day operating reserve so that `Available` and `SpendRoom` never spend accrued tax money on non-deductible outflows:

$$\text{TotalReserve}(\text{dailyCost},\ \text{taxReserve}) = \big(\max(0,\ \text{dailyCost}) \times \text{ReserveDays}\ (7)\big) + \max(0,\ \text{taxReserve})$$

$$\text{Available}(\text{cash},\ \text{dailyCost},\ \text{taxReserve}) = \max\big(0,\ \text{cash} - \text{TotalReserve}(\text{dailyCost},\ \text{taxReserve})\big)$$

> **Crucial Exception for Tax-Deductible CapEx:** When evaluating a **100% tax-deductible CapEx purchase** of cost $K$ (e.g., buying a Freight Truck or Pallet Shelves), spending $K$ reduces `UnbilledAccruedTax` by $K \times \text{EstimatedTaxRate}$. Therefore, the net cash impact against `TotalReserve` is only $K \times (1 - \text{EstimatedTaxRate})$!

---

## 3. Master Decision Tables: Tax Payment & Pre-Tax CapEx Acceleration

### Table 3A — `TaxPolicy.DecidePayment` (Full vs. Partial Payment on `day.rolled`)

| Tax State (`BilledCurrentTax`, `BilledBackTax`) | Cash vs. Operating Floor (`MinSurvivalDays = 3`) | Action (`TaxPaymentPick`) | Amount Paid | Why |
| :--- | :--- | :--- | :--- | :--- |
| **`BilledBackTax > 0` (Overdue Back Taxes)** | `cash - (dailyCost * 3) >= BilledBackTax` | **`pay_back_full`** | `BilledBackTax` | Eliminates highest-priority overdue debt immediately while keeping $\ge 3$ days of survival payroll/rent. |
| **`BilledBackTax > 0` (Tight Cash)** | `cash - (dailyCost * 3) < BilledBackTax` AND `> MinTaxPartialStep` ($\$500$) | **`pay_back_partial`** | `cash - (dailyCost * 3)` | Uses 1.0 partial tax payment to shrink the overdue principal without bankrupting tomorrow's store payroll/rent. |
| **`BilledCurrentTax > 0` (Normal / Before Due Day)** | `cash - (dailyCost * 7) >= BilledCurrentTax` | **`pay_current_full`** | `BilledCurrentTax` | Pays $100\%$ of current tax bill comfortably above the 7-day operating reserve. |
| **`BilledCurrentTax > 0` & Due Within `UrgentTaxDays = 2`** | `cash - (dailyCost * 3) < BilledCurrentTax` (Cannot pay full without dipping into 7d reserve) | **`pay_current_partial`** | $\max\big(0,\ \min(\text{BilledCurrentTax},\ \text{cash} - \text{dailyCost} \times 3)\big)$ | Dips down to the 3-day survival floor (`MinSurvivalDays = 3`) to pay as much of the current tax bill as possible before the **10% late penalty** hits! |
| **`BilledCurrentTax == 0 && BilledBackTax == 0`** | Any | **`none`** | $\$0$ | Keep `UnbilledAccruedTax` locked inside `TotalReserve`. |

### Table 3B — Pre-Tax CapEx Acceleration Window (`TaxShieldWindowDays = 5`)

When `daysUntilTaxAssessment <= TaxShieldWindowDays` ($5$) AND `TaxableCycleIncome > 0m`:

| Planned CapEx Category | Normal Spend Cap (`DaySpend`) | Pre-Tax Shield Spend Cap (`TaxShieldDaySpend = 0.60m`) | Qualification Predicate | Tax Shield Benefit |
| :--- | :--- | :--- | :--- | :--- |
| **1. Delivering Warehouse Freight Trucks (`Trucks.Decide`)** | $100\%$ above `7d` reserve | $100\%$ above `7d` operating reserve (unlocks accrued tax portion!) | `emptySlots > 0` on a delivering WH | Deducts $\$45,000\text{–}\$65,000$ truck purchase from `TaxableCycleIncome`, saving $\sim \$11,250\text{–}\$16,250$ in cash taxes. |
| **2. Warehouse Pallet Shelf Expansion (`WarehouseShelves`)** | $25\%$ (`DaySpend`) | **$60\%$ (`TaxShieldDaySpend`)** | `UsedBoxes / (Shelves * 60) >= 0.70` (lowered from $0.80$ inside tax window) | Buying a 10-shelf block ($\$25,000$) before tax day costs only $\$18,750$ net of the $25\%$ tax deduction. |
| **3. Stage 4 Building Purchases (`RealEstate.Decide`)** | $25\%$ (`DaySpend`) | **$60\%$ (`TaxShieldDaySpend`)** | `Stage == 4 && PaybackDays <= PaybackMax` ($365$) | Accelerates HQ/Warehouse buyouts inside the 5-day window before tax assessment. |
| **4. Non-Deductible / Operating Outflows (Loan Repay)** | $100\%$ above `RepayFloor` | **Unchanged** (Principal loan repayment is *not* a tax deduction) | Normal `Money.Decide` rules | Never raid `TaxReserve` to repay low-interest bank loan principal right before tax day. |

---

## 4. Complete C# Implementation (`src/ChaoticCEO/TaxPolicy.cs`)

```csharp
using System;

namespace ChaoticCEO
{
    public sealed class TaxPaymentPick
    {
        public TaxPaymentPick(string kind, decimal amount, bool chosen)
        {
            Kind = kind ?? "";
            Amount = amount;
            Chosen = chosen;
        }

        public string Kind { get; }

        public decimal Amount { get; }

        public bool Chosen { get; }

        public static TaxPaymentPick None()
        {
            return new TaxPaymentPick("", 0m, false);
        }
    }

    public static class TaxPolicy
    {
        public const decimal EstimatedTaxRate = 0.25m;

        public const decimal LateTaxPenaltyRate = 0.10m;

        public const int UrgentTaxDays = 2;

        public const int MinSurvivalDays = 3;

        public const decimal MinTaxPartialStep = 500m;

        public const int TaxShieldWindowDays = 5;

        public const decimal TaxShieldDaySpend = 0.60m;

        public static decimal UnbilledAccruedTax(
            decimal cycleNetProfit,
            decimal deductibleCapEx = 0m,
            decimal estimatedRate = EstimatedTaxRate)
        {
            var taxable = cycleNetProfit - (deductibleCapEx > 0m ? deductibleCapEx : 0m);
            if (taxable <= 0m || estimatedRate <= 0m)
            {
                return 0m;
            }

            return decimal.Round(taxable * estimatedRate, 2, MidpointRounding.AwayFromZero);
        }

        public static decimal TotalTaxReserve(
            decimal billedCurrentTax,
            decimal billedBackTax,
            decimal cycleNetProfit,
            decimal deductibleCapEx = 0m)
        {
            var billed = Math.Max(0m, billedCurrentTax) + Math.Max(0m, billedBackTax);
            var accrued = UnbilledAccruedTax(cycleNetProfit, deductibleCapEx);
            return billed + accrued;
        }

        public static decimal EffectiveSpendRoom(
            decimal cash,
            decimal dailyCost,
            decimal taxReserve,
            bool isDeductibleCapEx,
            int daysUntilTaxAssessment)
        {
            var operatingReserve = Money.Reserve(dailyCost);
            var effectiveTaxReserve = Math.Max(0m, taxReserve);

            // If spending on 100% tax-deductible CapEx, the tax reserve requirement shrinks proportionally.
            if (isDeductibleCapEx && effectiveTaxReserve > 0m)
            {
                effectiveTaxReserve = decimal.Round(
                    effectiveTaxReserve * (1m - EstimatedTaxRate),
                    2,
                    MidpointRounding.AwayFromZero);
            }

            var above = cash - operatingReserve - effectiveTaxReserve;
            if (above <= 0m)
            {
                return 0m;
            }

            var inShieldWindow = isDeductibleCapEx
                && daysUntilTaxAssessment >= 0
                && daysUntilTaxAssessment <= TaxShieldWindowDays;
            var rate = inShieldWindow ? TaxShieldDaySpend : Money.DaySpend;
            return decimal.Round(above * rate, 2, MidpointRounding.AwayFromZero);
        }

        public static TaxPaymentPick DecidePayment(
            decimal cash,
            decimal dailyCost,
            decimal billedCurrentTax,
            decimal billedBackTax,
            int daysUntilDue = 0)
        {
            var survivalFloor = Math.Max(0m, dailyCost) * MinSurvivalDays;
            var reserveFloor = Money.Reserve(dailyCost);
            var survivalRoom = cash - survivalFloor;
            var reserveRoom = cash - reserveFloor;

            // 1. Back taxes always take priority to stop compounding penalties.
            if (billedBackTax > 0m)
            {
                if (survivalRoom >= billedBackTax)
                {
                    return new TaxPaymentPick("back", billedBackTax, true);
                }

                if (survivalRoom >= MinTaxPartialStep)
                {
                    return new TaxPaymentPick("back-partial", survivalRoom, true);
                }

                return TaxPaymentPick.None();
            }

            // 2. Current billed taxes.
            if (billedCurrentTax > 0m)
            {
                if (reserveRoom >= billedCurrentTax)
                {
                    return new TaxPaymentPick("current", billedCurrentTax, true);
                }

                // If due within UrgentTaxDays (<= 2 days), pay down to the 3-day survival floor to avoid the 10% late penalty.
                if (daysUntilDue <= UrgentTaxDays)
                {
                    if (survivalRoom >= billedCurrentTax)
                    {
                        return new TaxPaymentPick("current", billedCurrentTax, true);
                    }

                    if (survivalRoom >= MinTaxPartialStep)
                    {
                        return new TaxPaymentPick("current-partial", survivalRoom, true);
                    }
                }
                else if (reserveRoom >= MinTaxPartialStep)
                {
                    return new TaxPaymentPick("current-partial", reserveRoom, true);
                }
            }

            return TaxPaymentPick.None();
        }
    }
}
```

---

## 5. Copy-Pasteable Tier-1 xUnit Locks (`src/ChaoticCEO.Tests/TaxPolicyTests.cs`)

```csharp
using Xunit;

namespace ChaoticCEO.Tests
{
    public sealed class TaxPolicyTests
    {
        [Fact]
        public void TaxPolicy_accrues_unbilled_tax_net_of_deductible_capex()
        {
            // $100,000 cycle net profit with $20,000 deductible shelf/truck CapEx -> $80,000 taxable * 25% = $20,000 accrued tax.
            var accrued = TaxPolicy.UnbilledAccruedTax(cycleNetProfit: 100_000m, deductibleCapEx: 20_000m);
            Assert.Equal(20_000m, accrued);

            var totalReserve = TaxPolicy.TotalTaxReserve(
                billedCurrentTax: 5_000m,
                billedBackTax: 0m,
                cycleNetProfit: 100_000m,
                deductibleCapEx: 20_000m);
            Assert.Equal(25_000m, totalReserve);
        }

        [Fact]
        public void TaxPolicy_makes_partial_payment_down_to_three_day_survival_floor_when_due_date_is_urgent()
        {
            // Cash = $12,000, DailyCost = $1,000 -> 7d reserve = $7,000, 3d survival floor = $3,000.
            // BilledCurrentTax = $10,000 due tomorrow (daysUntilDue = 1 <= UrgentTaxDays 2).
            // Full payment ($10,000) would leave $2,000 (< $3,000 survival floor).
            // Partial payment pays $12,000 - $3,000 = $9,000 so only $1,000 faces the 10% late penalty!
            var pick = TaxPolicy.DecidePayment(
                cash: 12_000m,
                dailyCost: 1_000m,
                billedCurrentTax: 10_000m,
                billedBackTax: 0m,
                daysUntilDue: 1);

            Assert.True(pick.Chosen);
            Assert.Equal("current-partial", pick.Kind);
            Assert.Equal(9_000m, pick.Amount);
        }

        [Fact]
        public void TaxPolicy_expands_spend_room_for_deductible_capex_inside_five_day_tax_shield_window()
        {
            // Cash = $100,000, DailyCost = $2,000 (7d reserve = $14,000), TaxReserve = $20,000.
            // Normal non-deductible spend room = ($100,000 - $14,000 - $20,000) * 0.25 = $16,500.
            var normalRoom = TaxPolicy.EffectiveSpendRoom(
                cash: 100_000m,
                dailyCost: 2_000m,
                taxReserve: 20_000m,
                isDeductibleCapEx: false,
                daysUntilTaxAssessment: 3);
            Assert.Equal(16_500m, normalRoom);

            // Deductible CapEx inside 5-day window: effectiveTaxReserve drops to $15,000, rate rises to 60% -> ($100k - $14k - $15k) * 0.60 = $42,600!
            var shieldRoom = TaxPolicy.EffectiveSpendRoom(
                cash: 100_000m,
                dailyCost: 2_000m,
                taxReserve: 20_000m,
                isDeductibleCapEx: true,
                daysUntilTaxAssessment: 3);
            Assert.Equal(42_600m, shieldRoom);
        }
    }
}
```