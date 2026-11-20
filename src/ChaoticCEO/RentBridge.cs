using Buildings;

namespace ChaoticCEO
{
    public static class RentBridge
    {
        public static bool Live(BuildingRent? rent)
        {
            if (rent == null || !rent.Rented || rent.Street.Length == 0 || rent.Number <= 0)
            {
                return false;
            }

            try
            {
                BuildingCommands.Command_RentBuilding(rent.Number, rent.Street);
                return true;
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "RentBridge.Live.1", "");
                return false;
            }
        }
    }
}
