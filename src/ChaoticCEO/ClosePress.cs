using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Entities;
using Helpers;

namespace ChaoticCEO
{
    public sealed class CloseButton
    {
        public CloseButton(string street, int number, string call)
        {
            Street = street ?? "";
            Number = number;
            Call = call ?? "";
        }

        public string Label
        {
            get { return "Close"; }
        }

        public string Street { get; }

        public int Number { get; }

        public string Call { get; }
    }

    public static class ClosePress
    {
        public static IReadOnlyList<string> Controls(bool armed)
        {
            if (armed)
            {
                return new[] { "Confirm", "Cancel" };
            }

            return new[] { "Close" };
        }

        public static IReadOnlyList<CloseButton> Buttons(ApiNote? api, CityBoard? board)
        {
            var buttons = new List<CloseButton>();
            if (api == null || api.Close.Length == 0 || board == null)
            {
                return buttons;
            }

            foreach (var step in board.Steps)
            {
                if (step == null || step.Kind != "close" || step.Street.Length == 0 || step.Number <= 0)
                {
                    continue;
                }

                buttons.Add(new CloseButton(step.Street, step.Number, api.Close));
            }

            return buttons;
        }

        public static StoreShutdown Press(
            string localAppData,
            ApiNote? api,
            CityBoard? board,
            string? street,
            int number,
            bool confirmed,
            Action<string, string, int>? bridge = null)
        {
            var shut = Shutdown.Confirm(
                api,
                board == null ? null : board.Steps,
                street,
                number,
                confirmed);
            if (!shut.Closed)
            {
                return shut;
            }

            if (bridge != null)
            {
                bridge(shut.Call, shut.Street, shut.Number);
                Shutdown.Record(localAppData ?? "", shut);
                return shut;
            }

            if (CloseBridge.Live(shut) || !CloseBridge.HasSave())
            {
                Shutdown.Record(localAppData ?? "", shut);
                return shut;
            }

            CloseBridge.NoteMiss(localAppData ?? "", shut);
            return shut;
        }
    }

    public static class CloseBridge
    {
        public static bool HasSave()
        {
            return SaveGameManager.Current != null;
        }

        public static void NoteMiss(string localAppData, StoreShutdown shut)
        {
            var path = LoadLog.CeoLogPath(localAppData ?? "");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var line = "store.missed version=" + LoadLog.Version
                + " street=" + shut.Street
                + " number=" + shut.Number.ToString(CultureInfo.InvariantCulture);
            File.AppendAllText(path, line + Environment.NewLine);
        }

        public static bool Live(StoreShutdown? shut)
        {
            if (shut == null || !shut.Closed)
            {
                return false;
            }

            var current = SaveGameManager.Current;
            if (current == null || current.BuildingRegistrations == null)
            {
                return false;
            }

            var matches = new List<BuildingRegistration>();
            foreach (var building in current.BuildingRegistrations)
            {
                if (building == null
                    || building.StreetName != shut.Street
                    || building.StreetNumber != shut.Number)
                {
                    continue;
                }

                matches.Add(building);
            }

            if (matches.Count == 0)
            {
                return false;
            }

            var named = false;
            foreach (var building in matches)
            {
                if (string.IsNullOrEmpty(building.BusinessName))
                {
                    continue;
                }

                named = true;
                Shut(building);
            }

            if (!named)
            {
                Shut(matches[0]);
            }

            return true;
        }

        private static void Shut(BuildingRegistration building)
        {
            BusinessHelper.ShutdownBusiness(building);
            building.AvailableForRent = true;
            try
            {
                var city = InstanceBehavior<CityManager>.Instance;
                if (city == null)
                {
                    return;
                }

                var controller = city.FindCityBuildingController(
                    new Address(building.StreetName, building.StreetNumber));
                controller?.UpdateSign();
            }
            catch (Exception)
            {
                // The business is already shut down. A missing sign refresh still leaves the data closed.
            }
        }
    }
}
