namespace ChaoticCEO
{
    public sealed class RemoteLayoutResult
    {
        public RemoteLayoutResult(string businessType, bool applied, bool silentNoop)
        {
            BusinessType = businessType ?? "";
            Applied = applied;
            SilentNoop = silentNoop;
        }

        public string BusinessType { get; }

        public bool Applied { get; }

        public bool SilentNoop { get; }
    }

    public static class RemoteLayout
    {
        public static RemoteLayoutResult Decide(
            bool entered,
            BuildingRent? rent,
            string? currentType,
            StarterPick? pick)
        {
            _ = entered;
            var current = Reading(currentType);
            if (rent == null || !rent.Rented || pick == null || !pick.Loaded)
            {
                return new RemoteLayoutResult(current, false, false);
            }

            var next = Reading(pick.BusinessType);
            if (next.Length == 0)
            {
                return new RemoteLayoutResult("", false, true);
            }

            return new RemoteLayoutResult(next, true, false);
        }

        public static string Open(
            string? localAppData,
            BuildingRent? rent,
            bool entered,
            string? currentType,
            StarterPick? pick,
            int day,
            string? writtenType = null)
        {
            var decided = Decide(entered, rent, currentType, pick);
            var street = rent == null ? "" : rent.Street;
            var number = rent == null ? 0 : rent.Number;
            var stuck = writtenType == null ? decided.BusinessType : Reading(writtenType);
            if (decided.SilentNoop || (decided.Applied && stuck.Length == 0))
            {
                var expected = decided.BusinessType;
                if (expected.Length == 0)
                {
                    expected = pick == null || Reading(pick.BusinessType).Length == 0
                        ? "business-type"
                        : Reading(pick.BusinessType);
                }

                return StateCheck.Record(
                    localAppData,
                    day,
                    "layout",
                    street,
                    number,
                    expected,
                    stuck);
            }

            if (!decided.Applied || pick == null || rent == null)
            {
                return "";
            }

            return Starter.Record(localAppData ?? "", pick, rent, day);
        }

        private static string Reading(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            var text = value.Trim();
            if (text == "ba:businesstype_empty")
            {
                return "";
            }

            return text;
        }
    }
}
