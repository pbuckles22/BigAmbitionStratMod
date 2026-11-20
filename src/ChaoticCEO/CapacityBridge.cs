using Helpers;

namespace ChaoticCEO
{
    public static class CapacityBridge
    {
        public static bool Live(BuildingRegistration? registration, CapacityPick? pick)
        {
            if (registration == null || pick == null || !pick.Matched)
            {
                return false;
            }

            try
            {
                BusinessHelper.UpdateCustomerCapacity(registration);
                return true;
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "CapacityBridge.Live.1", "");
                return false;
            }
        }

        public static bool AlreadyMatched(string localAppData, string street, int number)
        {
            var path = LoadLog.CeoLogPath(localAppData ?? "");
            if (!System.IO.File.Exists(path) || street.Length == 0 || number <= 0)
            {
                return false;
            }

            var needle = " street=" + street + " number=" + number.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ";
            foreach (var line in System.IO.File.ReadAllLines(path))
            {
                if (line.StartsWith("capacity.matched ", System.StringComparison.Ordinal) && line.Contains(needle))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
