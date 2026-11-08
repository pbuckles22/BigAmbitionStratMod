using System.Collections.Generic;
using Buildings;
using Entities;
using Helpers;
using SpecialServices.Bank;
using UnityEngine;

namespace ChaoticCEO
{
    public static class BankLoanBridge
    {
        public static bool HasLoan()
        {
            var current = SaveGameManager.Current;
            return current?.Loans != null && current.Loans.Count > 0;
        }

        public static IReadOnlyList<LoanOffer> Offers(decimal projectedDailyProfit)
        {
            var offers = new List<LoanOffer>();
            var bank = FirstBank();
            if (bank.settings == null || projectedDailyProfit <= 0m)
            {
                return offers;
            }

            var max = Mathf.FloorToInt(bank.settings.maxTotalLoanAmount);
            if (max < (int)BankLoan.MinimumAmount)
            {
                return offers;
            }

            var cover = projectedDailyProfit * BankLoan.LoanCover;
            var candidates = new[] { (int)BankLoan.MinimumAmount, max / 2, max };
            foreach (var amount in candidates)
            {
                if (amount < (int)BankLoan.MinimumAmount)
                {
                    continue;
                }

                var daily = LoanHelper.CalculateDailyPayment(amount, bank.settings);
                if (daily <= 0 || daily > cover)
                {
                    continue;
                }

                offers.Add(new LoanOffer(amount, daily));
            }

            return offers;
        }

        public static bool Take(LoanTakePick? pick)
        {
            if (pick == null || !pick.Taken || pick.Amount < BankLoan.MinimumAmount)
            {
                return false;
            }

            var current = SaveGameManager.Current;
            var bank = FirstBank();
            if (current == null || bank.address == null || bank.settings == null)
            {
                return false;
            }

            try
            {
                if (current.Loans == null)
                {
                    current.Loans = new List<Loan>();
                }

                var amount = (float)pick.Amount;
                var interest = LoanHelper.CalculateDailyInterestPayment(amount, bank.settings);
                var loan = new Loan(amount, interest, pick.DailyPayment, bank.address);
                current.Loans.Add(loan);
                var data = new Dictionary<string, string>
                {
                    {
                        "businessName",
                        BuildingHelper.GetBuildingRegistration(bank.address)?.BusinessName ?? "Bank"
                    },
                };
                var info = new TransactionInfo("ba:transaction_loanpayout", data, isTaxDeductible: false);
                GameManager.ChangeMoneySafe(amount, info, null, null, force: true);
                SaveGameManager.MarkChange();
                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        public static bool Repay(LoanRepayPick? pick)
        {
            if (pick == null || !pick.Repaid || pick.Amount <= 0m)
            {
                return false;
            }

            var current = SaveGameManager.Current;
            if (current?.Loans == null || current.Loans.Count == 0)
            {
                return false;
            }

            try
            {
                var loan = current.Loans[0];
                var amount = Mathf.Min((float)pick.Amount, loan.remainingAmount);
                var bankName = BuildingHelper.GetBuildingRegistration(loan.bankAddress)?.BusinessName ?? "Bank";
                var info = new TransactionInfo(
                    "ba:transaction_loanpayoff",
                    new Dictionary<string, string> { { "businessName", bankName } },
                    isTaxDeductible: false);
                if (!GameManager.ChangeMoneySafe(-amount, info, null, null, force: false, showNotification: true))
                {
                    return false;
                }

                loan.remainingAmount -= amount;
                if (loan.remainingAmount <= 0f)
                {
                    current.Loans.Remove(loan);
                }

                SaveGameManager.MarkChange();
                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private static (Address? address, BankSettings? settings) FirstBank()
        {
            var current = SaveGameManager.Current;
            if (current?.BuildingRegistrations == null)
            {
                return (null, null);
            }

            foreach (var registration in current.BuildingRegistrations)
            {
                if (registration?.Address == null)
                {
                    continue;
                }

                var settings = LoanHelper.GetBankSettings(registration.Address);
                if (settings != null)
                {
                    return (registration.Address, settings);
                }
            }

            return (null, null);
        }
    }
}
