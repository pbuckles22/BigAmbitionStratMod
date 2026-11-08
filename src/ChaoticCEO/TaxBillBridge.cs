using Helpers;

namespace ChaoticCEO
{
    public static class TaxBillBridge
    {
        public static (decimal current, decimal back) Due()
        {
            try
            {
                var current = TaxHelper.HasCurrentTaxesToPay()
                    ? (decimal)TaxHelper.GetCurrentTaxesToPay()
                    : 0m;
                var back = TaxHelper.HasBackTaxesToPay()
                    ? (decimal)TaxHelper.GetBackTaxesToPay()
                    : 0m;
                return (current, back);
            }
            catch (System.Exception)
            {
                return (0m, 0m);
            }
        }

        public static bool Live(TaxPick? pick)
        {
            if (pick == null || !pick.Paid)
            {
                return false;
            }

            try
            {
                var ok = true;
                if (pick.Current > 0m)
                {
                    ok = TaxHelper.PayCurrentTaxes((float)pick.Current) && ok;
                }

                if (pick.Back > 0m)
                {
                    ok = TaxHelper.PayBackTaxes((float)pick.Back) && ok;
                }

                return ok;
            }
            catch (System.Exception)
            {
                return false;
            }
        }
    }
}
