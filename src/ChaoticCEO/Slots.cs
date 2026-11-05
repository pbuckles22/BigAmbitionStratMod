using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class GarageSlot
    {
        public GarageSlot(string warehouse, int index, bool filled, string driver = "")
        {
            Warehouse = warehouse ?? "";
            Index = index;
            Filled = filled;
            Driver = driver ?? "";
        }

        public string Warehouse { get; }

        public int Index { get; }

        public bool Filled { get; }

        public string Driver { get; }
    }

    public sealed class SlotFill
    {
        public SlotFill(string warehouse, int index, string order, string assign, string driver)
        {
            Warehouse = warehouse ?? "";
            Index = index;
            Order = order ?? "";
            Assign = assign ?? "";
            Driver = driver ?? "";
        }

        public string Warehouse { get; }

        public int Index { get; }

        public string Order { get; }

        public string Assign { get; }

        public string Driver { get; }
    }

    public static class Slots
    {
        public const string DriverSkill = "ba:skill_deliverydriver";

        public static IReadOnlyList<SlotFill> Fill(
            ApiNote? api,
            IReadOnlyList<GarageSlot>? slots,
            IReadOnlyList<string>? drivers)
        {
            var order = api == null ? "" : api.Call("truck buy");
            var assign = api == null ? "" : api.Call("slot assign");
            var bought = new List<SlotFill>();
            if (order.Length == 0 || assign.Length == 0 || slots == null)
            {
                return bought;
            }

            var pool = drivers ?? Array.Empty<string>();
            var nextDriver = 0;
            foreach (var slot in slots)
            {
                if (slot == null || slot.Filled || slot.Warehouse.Length == 0 || slot.Index <= 0)
                {
                    continue;
                }

                var driver = "";
                if (nextDriver < pool.Count && pool[nextDriver] != null && pool[nextDriver].Length > 0)
                {
                    driver = pool[nextDriver];
                    nextDriver++;
                }

                bought.Add(new SlotFill(slot.Warehouse, slot.Index, order, assign, driver));
            }

            return bought;
        }

        public static string Line(int day, SlotFill fill)
        {
            return "slot.filled version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " warehouse=" + fill.Warehouse
                + " slot=" + fill.Index.ToString(CultureInfo.InvariantCulture)
                + " order=" + fill.Order
                + " assign=" + fill.Assign
                + " driver=" + fill.Driver;
        }

        public static string Summary(int day, int bought)
        {
            return "slot.summary version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " bought=" + bought.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class SlotWatch
    {
        private object? _seen;

        public IReadOnlyList<string> Tick(
            string localAppData,
            object? save,
            int day,
            ApiNote? api,
            IReadOnlyList<GarageSlot>? slots,
            IReadOnlyList<string>? drivers)
        {
            if (save == null || ReferenceEquals(_seen, save))
            {
                return Array.Empty<string>();
            }

            _seen = save;
            var fills = Slots.Fill(api, slots, drivers);
            var lines = new List<string>();
            foreach (var fill in fills)
            {
                lines.Add(Slots.Line(day, fill));
            }

            if (lines.Count == 0)
            {
                lines.Add(Slots.Summary(day, 0));
            }

            var path = LoadLog.CeoLogPath(localAppData ?? "");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, string.Join(System.Environment.NewLine, lines) + System.Environment.NewLine);
            return lines;
        }
    }
}
