using System.Collections.Generic;

namespace ChaoticCEO
{
    public static class SkuScope
    {
        public static bool IsBusinessAllowed(string? businessType, bool factoryPipeline = false)
        {
            var type = businessType ?? "";
            if (type.Length == 0)
            {
                return false;
            }

            if (!factoryPipeline && type == "ba:businesstype_electronicsstore")
            {
                return false;
            }

            return true;
        }

        public static IReadOnlyList<string> Order(IReadOnlyList<string>? items, bool factoryPipeline = false)
        {
            if (items == null || items.Count == 0)
            {
                return System.Array.Empty<string>();
            }

            var kept = new List<string>();
            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item))
                {
                    continue;
                }

                if (!factoryPipeline && FactoryLocked(item))
                {
                    continue;
                }

                kept.Add(item);
            }

            return kept;
        }

        private static bool FactoryLocked(string item)
        {
            var name = item;
            const string prefix = "ba:itemname_";
            if (name.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                name = name.Substring(prefix.Length);
            }

            switch (name)
            {
                case "smartphone1":
                case "smartphone2":
                case "earbuds01":
                case "headphones":
                case "headphones01":
                case "smartwatch":
                case "smartwatch1":
                case "smartwatch2":
                    return true;
                default:
                    return false;
            }
        }
    }
}
