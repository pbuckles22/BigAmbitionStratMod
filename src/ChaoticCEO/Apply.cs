using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class PriceApply
    {
        public PriceApply(string item, float price, bool applied, string call)
        {
            Item = item ?? "";
            Price = price;
            Applied = applied;
            Call = call ?? "";
        }

        public string Item { get; }

        public float Price { get; }

        public bool Applied { get; }

        public string Call { get; }
    }

    public sealed class StaffApply
    {
        public StaffApply(string role, string kind, string planId, int amount, bool applied, string call)
        {
            Role = role ?? "";
            Kind = kind ?? "";
            PlanId = planId ?? "";
            Amount = amount;
            Applied = applied;
            Call = call ?? "";
        }

        public string Role { get; }

        public string Kind { get; }

        public string PlanId { get; }

        public int Amount { get; }

        public bool Applied { get; }

        public string Call { get; }
    }

    public static class Apply
    {
        public static PriceApply ConfirmPrice(ApiNote? api, SoldPrice? price, string? item, bool confirmed)
        {
            if (!confirmed
                || api == null
                || api.Price.Length == 0
                || price == null
                || string.IsNullOrEmpty(item)
                || price.Item != item
                || price.Price <= 0f)
            {
                return NonePrice();
            }

            return new PriceApply(price.Item, price.Price, true, api.Price);
        }

        public static StaffApply ConfirmStaff(ApiNote? api, StaffPlan? plan, string? planId, bool confirmed)
        {
            if (!confirmed
                || api == null
                || plan == null
                || string.IsNullOrEmpty(planId)
                || plan.PlanId != planId
                || plan.Amount == 0
                || plan.Role.Length == 0
                || (plan.Kind != "hr" && plan.Kind != "headhunter"))
            {
                return NoneStaff();
            }

            return new StaffApply(plan.Role, plan.Kind, plan.PlanId, plan.Amount, true, "");
        }

        public static string RecordPrice(string localAppData, PriceApply? apply)
        {
            if (apply == null || !apply.Applied)
            {
                return "";
            }

            return Append(
                localAppData,
                "price.applied version=" + LoadLog.Version
                    + " item=" + apply.Item
                    + " price=" + apply.Price.ToString(CultureInfo.InvariantCulture)
                    + " call=" + apply.Call);
        }

        public static string RecordStaff(string localAppData, StaffApply? apply)
        {
            if (apply == null || !apply.Applied)
            {
                return "";
            }

            return Append(
                localAppData,
                "staff.applied version=" + LoadLog.Version
                    + " role=" + apply.Role
                    + " kind=" + apply.Kind
                    + " plan=" + apply.PlanId
                    + " amount=" + apply.Amount.ToString(CultureInfo.InvariantCulture));
        }

        public static PriceApply ReloadPrice(PriceApply? apply)
        {
            return NonePrice();
        }

        public static StaffApply ReloadStaff(StaffApply? apply)
        {
            return NoneStaff();
        }

        private static string Append(string localAppData, string line)
        {
            var path = LoadLog.CeoLogPath(localAppData);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + System.Environment.NewLine);
            return line;
        }

        private static PriceApply NonePrice()
        {
            return new PriceApply("", 0f, false, "");
        }

        private static StaffApply NoneStaff()
        {
            return new StaffApply("", "", "", 0, false, "");
        }
    }
}
