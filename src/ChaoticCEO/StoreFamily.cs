using System.Collections.Generic;

namespace ChaoticCEO
{
    public static class StoreFamily
    {
        public static string? For(string? businessType)
        {
            if (string.IsNullOrEmpty(businessType))
            {
                return null;
            }

            return Families.TryGetValue(businessType, out var family) ? family : null;
        }

        private static readonly Dictionary<string, string> Families = new Dictionary<string, string>
        {
            { "ba:businesstype_fastfoodrestaurant", "food" },
            { "ba:businesstype_coffeeshop", "food" },
            { "ba:businesstype_fruitandvegetablestore", "food" },
            { "ba:businesstype_liquorstore", "food" },
            { "ba:businesstype_clothingstore", "durables" },
            { "ba:businesstype_electronicsstore", "durables" },
            { "ba:businesstype_furniturestore", "durables" },
            { "ba:businesstype_cardealership", "durables" },
            { "ba:businesstype_florist", "specialty" },
            { "ba:businesstype_hairdresser", "service" },
            { "ba:businesstype_gym", "service" },
            { "ba:businesstype_nightclub", "service" },
            { "ba:businesstype_travelagency", "service" },
            { "ba:businesstype_eventplanningagency", "service" },
            { "ba:businesstype_lawfirm", "office" },
            { "ba:businesstype_recruitmentagency", "office" },
            { "ba:businesstype_webdevelopmentagency", "office" },
            { "ba:businesstype_wholesalestore", "warehouse" },
            { "ba:businesstype_truckgarage", "warehouse" },
        };
    }
}
