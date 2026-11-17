using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public enum BusinessMode
    {
        Retail = 0,
        Service = 1,
        Workstation = 2,
    }

    public static class Modes
    {
        public const string HairCare = "ba:itemname_haircareproduct";

        public const string StylingChair = "ba:itemname_hairdresserchairmodern";

        public const string Computer = "ba:itemname_computer";

        public const int OfficeDesks = 4;

        public const int GymDesks = 1;

        public static BusinessMode Of(string? businessType)
        {
            switch (businessType ?? "")
            {
                case "ba:businesstype_lawfirm":
                case "ba:businesstype_webdevelopmentagency":
                case "ba:businesstype_graphicsdesignagency":
                case "ba:businesstype_recruitmentagency":
                case "ba:businesstype_gym":
                case "ba:businesstype_travelagency":
                    return BusinessMode.Workstation;
                case "ba:businesstype_hairdresser":
                case "ba:businesstype_nightclub":
                    return BusinessMode.Service;
                default:
                    return BusinessMode.Retail;
            }
        }

        public static int DeskFloor(string? businessType)
        {
            if (Of(businessType) != BusinessMode.Workstation)
            {
                return 0;
            }

            if ((businessType ?? "") == "ba:businesstype_gym")
            {
                return GymDesks;
            }

            return StoreFamily.For(businessType) == "office" ? OfficeDesks : GymDesks;
        }

        public static bool SignsContract(string? businessType)
        {
            return Of(businessType) != BusinessMode.Workstation;
        }

        public static bool Stocked(
            BusinessMode mode,
            bool shelvesStocked,
            int hairCareOnHand,
            int stylingStations,
            int completeWorkstations,
            string? businessType)
        {
            if (!string.IsNullOrEmpty(businessType))
            {
                mode = Of(businessType);
            }

            if (mode == BusinessMode.Workstation)
            {
                var floor = DeskFloor(businessType);
                return floor > 0 && completeWorkstations >= floor;
            }

            if (mode == BusinessMode.Service && (businessType ?? "") == "ba:businesstype_hairdresser")
            {
                return hairCareOnHand > 0 && stylingStations >= 1;
            }

            return shelvesStocked;
        }

        public static string FitOut(string? businessType)
        {
            var type = businessType ?? "";
            foreach (var offer in Starter.StageOne())
            {
                if (offer != null && offer.BusinessType == type && offer.Layout.Length > 0)
                {
                    return offer.Layout;
                }
            }

            switch (type)
            {
                case "ba:businesstype_hairdresser":
                    return "HairdresserWholesalers";
                case "ba:businesstype_lawfirm":
                    return "LawFirm";
                case "ba:businesstype_webdevelopmentagency":
                    return "WebDevelopmentAgency";
                case "ba:businesstype_graphicsdesignagency":
                    return "GraphicDesigner";
                case "ba:businesstype_gym":
                    return "GymWholesalers";
                default:
                    return "";
            }
        }

        public static StationCount Read(string? businessType, IEnumerable<string>? placed)
        {
            var styling = 0;
            var desks = 0;
            var computers = 0;
            var seats = 0;
            var gym = 0;
            if (placed != null)
            {
                foreach (var name in placed)
                {
                    if (string.IsNullOrEmpty(name))
                    {
                        continue;
                    }

                    if (name == StylingChair)
                    {
                        styling++;
                    }
                    else if (name.StartsWith("ba:itemname_officedesk", StringComparison.Ordinal))
                    {
                        desks++;
                    }
                    else if (name == Computer)
                    {
                        computers++;
                    }
                    else if (name.StartsWith("ba:itemname_officechair", StringComparison.Ordinal)
                        || name.StartsWith("ba:itemname_armchair", StringComparison.Ordinal))
                    {
                        seats++;
                    }
                    else if (name == "ba:itemname_treadmill" || name == "ba:itemname_squatsstation")
                    {
                        gym++;
                    }
                }
            }

            var workstations = 0;
            if ((businessType ?? "") == "ba:businesstype_gym")
            {
                workstations = gym;
            }
            else if (Of(businessType) == BusinessMode.Workstation)
            {
                workstations = desks;
                if (computers < workstations)
                {
                    workstations = computers;
                }

                if (seats < workstations)
                {
                    workstations = seats;
                }
            }

            return new StationCount(styling, workstations);
        }

        public static decimal Labor(decimal hourlyWage, decimal unitsPerHour, decimal consumable)
        {
            if (hourlyWage <= 0m || unitsPerHour <= 0m)
            {
                return 0m;
            }

            var extra = consumable > 0m ? consumable : 0m;
            return hourlyWage / unitsPerHour + extra;
        }

        public static decimal Bill(BusinessMode mode, decimal labor, decimal overhead, decimal margin)
        {
            if (mode == BusinessMode.Retail || margin <= 0m || margin >= 1m)
            {
                return 0m;
            }

            if (labor < 0m)
            {
                labor = 0m;
            }

            if (overhead < 0m)
            {
                overhead = 0m;
            }

            return decimal.Round((labor + overhead) / (1m - margin), 2, MidpointRounding.AwayFromZero);
        }

        public static string Fee(string? businessType)
        {
            switch (businessType ?? "")
            {
                case "ba:businesstype_hairdresser":
                    return "ba:itemname_haircuttingfee";
                case "ba:businesstype_lawfirm":
                    return "ba:itemname_hourlylawyerfee";
                case "ba:businesstype_webdevelopmentagency":
                    return "ba:itemname_hourlyprogrammerfee";
                case "ba:businesstype_graphicsdesignagency":
                    return "ba:itemname_hourlygraphicdesignerfee";
                case "ba:businesstype_gym":
                    return "ba:itemname_gymcovercharge";
                default:
                    return "";
            }
        }

        public static PricePick Quote(string? businessType, decimal labor, decimal overhead, decimal margin)
        {
            var item = Fee(businessType);
            var bill = Bill(Of(businessType), labor, overhead, margin);
            if (item.Length == 0 || bill <= 0m)
            {
                return PricePick.None();
            }

            return new PricePick(item, bill, true);
        }

        public static PricePick Standard(string? businessType)
        {
            switch (businessType ?? "")
            {
                case "ba:businesstype_hairdresser":
                    return Quote(businessType, Labor(22m, 2m, 2.50m), 3.50m, 0.50m);
                case "ba:businesstype_lawfirm":
                    return Quote(businessType, Labor(60m, 1m, 0m), 8.50m, 0.50m);
                case "ba:businesstype_webdevelopmentagency":
                    return Quote(businessType, Labor(45m, 1m, 0m), 7.50m, 0.50m);
                case "ba:businesstype_graphicsdesignagency":
                    return Quote(businessType, Labor(36m, 1m, 0m), 6.50m, 0.50m);
                case "ba:businesstype_gym":
                    return Quote(businessType, Labor(18m, 10m, 0m), 2.20m, 0.50m);
                default:
                    return PricePick.None();
            }
        }

        public static string Record(string localAppData, PricePick? quote, int day)
        {
            if (quote == null || !quote.Chosen || quote.Item.Length == 0 || quote.Price <= 0m)
            {
                return "";
            }

            var line = "bill.quoted version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " item=" + quote.Item
                + " price=" + quote.Price.ToString("0.00", CultureInfo.InvariantCulture);
            var path = LoadLog.CeoLogPath(localAppData ?? "");
            if (File.Exists(path))
            {
                foreach (var existing in File.ReadAllLines(path))
                {
                    if (existing == line)
                    {
                        return line;
                    }
                }
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + Environment.NewLine);
            return line;
        }
    }

    public sealed class StationCount
    {
        public StationCount(int styling, int workstations)
        {
            Styling = styling;
            Workstations = workstations;
        }

        public int Styling { get; }

        public int Workstations { get; }
    }
}
