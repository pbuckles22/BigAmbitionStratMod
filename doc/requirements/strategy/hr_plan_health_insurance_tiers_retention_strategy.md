# HR Plan Configuration, Health Insurance Tiers & Retention Strategy (Exhaustive Engineering Spec)

## 1. Executive Problem Statement: Why `GiveBonus` Alone Fails in *Big Ambitions 1.0*

In `continuous_monitoring_self_heal_strategy_detailed.md` and `overall_growth_strategy_stage_1_to_4.md`:
* The monitor clears unmet employee demands after **`DemandDays`** ($3$) via `EmployeeInstance.GiveBonus` (`F15` `unmetDemand`).
* `Managers.Decide` scales $1$ HR Manager per **`HrSpan`** ($25$) employees and $1$ Headhunter per **`HuntSpan`** ($5$) open roles, and trains employees to $100\%$ in Stage 4.

In *Big Ambitions 1.0* (Build 3682, Charlie Pryor Ep. 01 & Ep. 07), relying solely on `GiveBonus` and Stage-4 training creates three severe failure modes:
1. **Structural Benefit Demands Cannot Be Cleared Permanently by Cash Bonuses:**
   * As employees gain skill ($> 50\%$ and $> 80\%$) or work full-time shifts, they roll persistent structural demands: **Health Insurance (`Bronze`, `Silver`, `Gold`)**, **Wage Raises**, or **Full-Time / Part-Time Hour Alignment**.
   * Calling `GiveBonus` grants a temporary $+10\%\text{–}+15\%$ satisfaction bump that decays while the underlying `EmployeeDemand` (e.g., `"Need Silver Health Insurance"`) remains unmet—causing the employee to resign!
2. **Sick Leave & Turnover Gaps Break `G3` (`Staffed`) on Single-Cashier Stores:**
   * Without an `HrManagerPlan` configured with health insurance and sick-leave replacement (`ReplaceSickEmployees = true`), a single sick cashier on a morning roll leaves a store with $0$ active register coverage, dropping `G4` (`Selling`) to $0$ customers for that day.
3. **Rival Employee Poaching at $8+$ District Businesses (`RivalPoachAttackThreshold = 8`):**
   * When the player reaches $8$ businesses in a neighborhood, the district Rival attempts to poach employees. Staff covered by **Gold Health Insurance** on an active `HrManagerPlan` with satisfaction $\ge$ **`AntiPoachSatisfactionFloor`** ($85\%$) are immune to rival poaching.

---

## 2. The 5-Pillar Autonomous HR & Retention Policy (`HrPolicy`)

### Pillar H1 — Stage-Gated Health Insurance Tier Escalation (`InsuranceTier`)
In `HrManagerPlan`, health insurance is assigned per employee group/plan at three cost tiers:
* **`None` ($\$0/\text{day}$):** Stage 1 bootstrap only (Days 1–15) before HQ and HR Manager exist. If an employee demands Bronze insurance in Stage 1 before HQ exists, satisfy the demand directly on the `EmployeeInstance` or bridge with `GiveBonus` until Stage 2.
* **`Bronze` ($\sim \$4\text{–}\$6/\text{employee/day}$):** Default baseline tier applied to all entry-level retail Cashiers, Cleaners, and Drivers immediately upon creating the first `HrManagerPlan` in **Stage 2**. Prevents $90\%$ of early resignations and reduces illness frequency.
* **`Silver` ($\sim \$10\text{–}\$14/\text{employee/day}$):** Applied in **Stage 3** to all Warehouse Drivers (`ba:skill_deliverydriver`), HQ Agents (`logistics`, `purchasing`, `hr`, `headhunter`), Office Specialists (`lawyer`, `webdeveloper`, `graphicdesigner`), and any employee whose skill reaches $\ge 75\%$.
* **`Gold` ($\sim \$22\text{–}\$28/\text{employee/day}$):** Applied automatically when **any** of three conditions holds:
  1. The employee's skill is $\ge 90\%$, OR
  2. The employee works in a neighborhood where `PlayerBusinessesInNeighborhood >= RivalExpansionAttackThreshold` ($6$) (pre-arming the anti-poaching shield before hitting $8$ businesses!), OR
  3. The empire is in **Stage 4** (`stage == 4`).

### Pillar H2 — Demand-Specific Resolution Matrix (Never Blindly Bonus a Structural Demand)
On every `day.rolled`, inspect each employee's active demands (`employee.Demands`) and `Satisfaction`:

| Demand Type / Symptom | Detection Condition | Exact Resolution Action | Fallback if HQ/HR Not Yet Open |
| :--- | :--- | :--- | :--- |
| **1. Health Insurance Demand (`Bronze` / `Silver` / `Gold`)** | Demand requires insurance tier $T_{\text{req}} > T_{\text{current}}$ | Upgrade employee's `HrManagerPlan` insurance tier to $\max(T_{\text{req}},\ \text{TargetTier})$ | Set individual insurance tier on `EmployeeInstance` |
| **2. Wage / Salary Demand (`Raise`)** | Employee demands hourly wage $W_{\text{req}} \le W_{\text{current}} \times \text{MaxRaiseCap}$ ($1.20$) | Grant wage raise to $W_{\text{req}}$ (`employee.HourlyWage = W_req`) | Same (always cheaper than losing a trained worker + paying headhunter fee) |
| **3. Hours Mismatch (`FullTime` vs. `PartTime`)** | Full-time worker ($30\text{–}40\text{h/wk}$) scheduled $< 30\text{h}$, or Part-time ($15\text{–}25\text{h/wk}$) scheduled $> 25\text{h}$ | Match shift template: `Mon–Fri 8–16` ($40\text{h/wk}$) for Full-Time; split `Mon–Wed 8–16` ($24\text{h}$) + `Thu–Sat 8–16` ($24\text{h}$) for two Part-Time workers | Hire 2nd part-time worker to split the 6-day `Mon–Sat` schedule ($24\text{h} + 24\text{h}$) |
| **4. General Low Satisfaction (`< 65%`) with No Structural Demand** | `Satisfaction < 65f && UnmetStructuralDemands == 0` | Call `EmployeeInstance.GiveBonus` (capped at once per **`BonusCooldownDays = 14`** per employee) | Same |

### Pillar H3 — Full-Time vs. Part-Time Shift Template Rules (`Mon–Sat 8–16` = $48\text{ Hours/Week}$)
Notice a critical arithmetic detail in *Big Ambitions*:
* A store open **Monday–Saturday `8:00–16:00` ($8\text{ hrs/day} \times 6\text{ days} = 48\text{ hrs/week}$)** exceeds a single Full-Time employee's preferred maximum ($40\text{ hrs/week}$) by $8$ hours if they have the `"No Overtime"` or `"Max 40 Hours"` demand!
* **The Shift Alignment Rule (`HrPolicy.BuildSchedule`):**
  1. **If employee has NO overtime restriction:** Assign `Mon–Sat 8–16` ($48\text{h/wk}$) with overtime pay enabled.
  2. **If Full-Time employee demands $\le 40\text{h/wk}$:** Assign Primary Employee to `Mon–Fri 8–16` ($40\text{h/wk}$) and assign a **Part-Time Floater/Weekend Cashier** to `Sat 8–16` (or `Fri–Sat 8–16`, $16\text{h/wk}$) across $1\text{–}2$ stores!
  3. **If only Part-Time candidates are available:** Pair **2 Part-Time employees** on the station: Worker A on `Mon, Tue, Wed 8–16` ($24\text{h/wk}$) and Worker B on `Thu, Fri, Sat 8–16` ($24\text{h/wk}$).

### Pillar H4 — Progressive Skill Training Curve (Don't Wait Until Stage 4 to Train Purchasing & Cashiers)
* Waiting until Stage 4 ($\$2\text{M}$ cash) to train employees forfeits massive Stage 2–3 margin gains:
  * **Stage 2 Training Target (`Stage2TrainTarget = 0.80f`):** Immediately train **Purchasing Agents** and **Logistics Managers** to $80\%$ (unlocking lower Importer unit costs and multi-building plan capacity), and train **Retail Cashiers** to $75\%$ (`PriceSatisfied` buffer).
  * **Stage 3 Training Target (`Stage3TrainTarget = 0.90f`):** Train all HQ agents, Drivers, and Office Specialists to $90\%$.
  * **Stage 4 Training Target (`Stage4TrainTarget = 1.00f`):** Train $100\%$ of staff across the empire to $100\%$ skill (`HrManagerPlan.TrainEmployees`).

### Pillar H5 — Automatic Sick-Cover & Turnover Float Pool (`BackupPool`)
* Every `HrManagerPlan` enables automatic sick-leave replacement and maintains **$1$ unassigned backup Cashier and $1$ unassigned backup Cleaner per `HrSpan` ($25$) employees** in Stage 2+ so an unexpected illness or resignation is swapped in on `day.rolled` in $0$ days.

---

## 3. Complete C# Implementation (`src/ChaoticCEO/HrPolicy.cs`)

```csharp
using System;

namespace ChaoticCEO
{
    public enum InsuranceTier
    {
        None = 0,
        Bronze = 1,
        Silver = 2,
        Gold = 3,
    }

    public sealed class EmployeeRetentionFacts
    {
        public EmployeeRetentionFacts(
            string employeeId,
            string role,
            decimal skill,
            decimal satisfaction,
            decimal currentWage,
            decimal demandedWage,
            InsuranceTier currentInsurance,
            InsuranceTier demandedInsurance,
            int playerBusinessesInDistrict,
            int daysSinceLastBonus)
        {
            EmployeeId = employeeId ?? "";
            Role = role ?? "";
            Skill = skill;
            Satisfaction = satisfaction;
            CurrentWage = currentWage;
            DemandedWage = demandedWage;
            CurrentInsurance = currentInsurance;
            DemandedInsurance = demandedInsurance;
            PlayerBusinessesInDistrict = playerBusinessesInDistrict;
            DaysSinceLastBonus = daysSinceLastBonus;
        }

        public string EmployeeId { get; }

        public string Role { get; }

        public decimal Skill { get; }

        public decimal Satisfaction { get; }

        public decimal CurrentWage { get; }

        public decimal DemandedWage { get; }

        public InsuranceTier CurrentInsurance { get; }

        public InsuranceTier DemandedInsurance { get; }

        public int PlayerBusinessesInDistrict { get; }

        public int DaysSinceLastBonus { get; }
    }

    public sealed class HrActionPick
    {
        public HrActionPick(
            string employeeId,
            string action,
            InsuranceTier targetInsurance,
            decimal targetWage,
            decimal targetSkill,
            bool chosen)
        {
            EmployeeId = employeeId ?? "";
            Action = action ?? "";
            TargetInsurance = targetInsurance;
            TargetWage = targetWage;
            TargetSkill = targetSkill;
            Chosen = chosen;
        }

        public string EmployeeId { get; }

        public string Action { get; }

        public InsuranceTier TargetInsurance { get; }

        public decimal TargetWage { get; }

        public decimal TargetSkill { get; }

        public bool Chosen { get; }

        public static HrActionPick None()
        {
            return new HrActionPick("", "", InsuranceTier.None, 0m, 0m, false);
        }
    }

    public static class HrPolicy
    {
        public const decimal Stage2TrainTarget = 0.80m;

        public const decimal Stage3TrainTarget = 0.90m;

        public const decimal Stage4TrainTarget = 1.00m;

        public const decimal MaxRaiseMultiplier = 1.20m;

        public const decimal BonusSatisfactionTrigger = 65m;

        public const int BonusCooldownDays = 14;

        public const int AntiPoachDistrictTrigger = 6;

        public static InsuranceTier TargetInsuranceTier(
            int stage,
            string? role,
            decimal skill,
            int playerBusinessesInDistrict,
            InsuranceTier demandedTier = InsuranceTier.None)
        {
            var policyTier = InsuranceTier.None;
            if (stage >= 4
                || skill >= 0.90m
                || playerBusinessesInDistrict >= AntiPoachDistrictTrigger)
            {
                policyTier = InsuranceTier.Gold;
            }
            else if (stage >= 3
                || skill >= 0.75m
                || IsSpecialistOrHqRole(role))
            {
                policyTier = stage >= 2 ? InsuranceTier.Silver : InsuranceTier.Bronze;
            }
            else if (stage >= 2)
            {
                policyTier = InsuranceTier.Bronze;
            }

            return demandedTier > policyTier ? demandedTier : policyTier;
        }

        public static decimal TargetTrainingSkill(int stage, string? role)
        {
            if (stage <= 1)
            {
                return 0m;
            }

            if (stage == 2)
            {
                return Stage2TrainTarget;
            }

            if (stage == 3)
            {
                return Stage3TrainTarget;
            }

            return Stage4TrainTarget;
        }

        public static HrActionPick DecideRetention(int stage, EmployeeRetentionFacts? emp)
        {
            if (emp == null || string.IsNullOrEmpty(emp.EmployeeId))
            {
                return HrActionPick.None();
            }

            var targetIns = TargetInsuranceTier(
                stage,
                emp.Role,
                emp.Skill,
                emp.PlayerBusinessesInDistrict,
                emp.DemandedInsurance);

            // 1. Upgrade health insurance if below policy target or employee demand.
            if (targetIns > emp.CurrentInsurance)
            {
                return new HrActionPick(
                    emp.EmployeeId,
                    "insurance",
                    targetIns,
                    emp.CurrentWage,
                    emp.Skill,
                    true);
            }

            // 2. Grant reasonable wage raise demand (<= 1.20x current wage).
            if (emp.DemandedWage > emp.CurrentWage
                && emp.CurrentWage > 0m
                && emp.DemandedWage <= emp.CurrentWage * MaxRaiseMultiplier)
            {
                return new HrActionPick(
                    emp.EmployeeId,
                    "raise",
                    emp.CurrentInsurance,
                    emp.DemandedWage,
                    emp.Skill,
                    true);
            }

            // 3. Queue training if below stage skill target.
            var trainTarget = TargetTrainingSkill(stage, emp.Role);
            if (trainTarget > emp.Skill)
            {
                return new HrActionPick(
                    emp.EmployeeId,
                    "train",
                    emp.CurrentInsurance,
                    emp.CurrentWage,
                    trainTarget,
                    true);
            }

            // 4. Cash bonus only when satisfaction is low (< 65%) and cooldown (>= 14d) has elapsed.
            if (emp.Satisfaction < BonusSatisfactionTrigger
                && emp.DaysSinceLastBonus >= BonusCooldownDays)
            {
                return new HrActionPick(
                    emp.EmployeeId,
                    "bonus",
                    emp.CurrentInsurance,
                    emp.CurrentWage,
                    emp.Skill,
                    true);
            }

            return HrActionPick.None();
        }

        private static bool IsSpecialistOrHqRole(string? role)
        {
            var r = role ?? "";
            return r == "purchasing"
                || r == "logistics"
                || r == "hr"
                || r == "headhunter"
                || r == "ba:skill_lawyer"
                || r == "ba:skill_webdeveloper"
                || r == "ba:skill_graphicdesigner"
                || r == "ba:skill_deliverydriver";
        }
    }
}
```

---

## 4. Copy-Pasteable Tier-1 xUnit Locks (`src/ChaoticCEO.Tests/HrPolicyTests.cs`)

```csharp
using Xunit;

namespace ChaoticCEO.Tests
{
    public sealed class HrPolicyTests
    {
        [Fact]
        public void HrPolicy_escalates_to_gold_insurance_when_district_reaches_six_stores_to_block_poaching()
        {
            // Stage 2 cashier with 60% skill normally gets Bronze, but at 6+ district stores gets Gold anti-poaching shield!
            Assert.Equal(
                InsuranceTier.Bronze,
                HrPolicy.TargetInsuranceTier(stage: 2, role: "cashier", skill: 0.60m, playerBusinessesInDistrict: 3));

            Assert.Equal(
                InsuranceTier.Gold,
                HrPolicy.TargetInsuranceTier(stage: 2, role: "cashier", skill: 0.60m, playerBusinessesInDistrict: 6));
        }

        [Fact]
        public void HrPolicy_satisfies_structural_insurance_and_wage_demands_before_falling_back_to_bonus()
        {
            var needsSilver = new EmployeeRetentionFacts(
                "emp_1",
                "cashier",
                skill: 0.78m,
                satisfaction: 52m,
                currentWage: 18m,
                demandedWage: 20m,
                currentInsurance: InsuranceTier.Bronze,
                demandedInsurance: InsuranceTier.Silver,
                playerBusinessesInDistrict: 2,
                daysSinceLastBonus: 30);

            // Priority 1: Upgrade insurance from Bronze -> Silver
            var step1 = HrPolicy.DecideRetention(stage: 2, needsSilver);
            Assert.True(step1.Chosen);
            Assert.Equal("insurance", step1.Action);
            Assert.Equal(InsuranceTier.Silver, step1.TargetInsurance);

            // Priority 2: Once on Silver, grant the $18 -> $20 wage raise (within 1.20x cap)
            var needsRaise = new EmployeeRetentionFacts(
                "emp_1",
                "cashier",
                skill: 0.78m,
                satisfaction: 52m,
                currentWage: 18m,
                demandedWage: 20m,
                currentInsurance: InsuranceTier.Silver,
                demandedInsurance: InsuranceTier.Silver,
                playerBusinessesInDistrict: 2,
                daysSinceLastBonus: 30);

            var step2 = HrPolicy.DecideRetention(stage: 2, needsRaise);
            Assert.True(step2.Chosen);
            Assert.Equal("raise", step2.Action);
            Assert.Equal(20m, step2.TargetWage);
        }
    }
}
```