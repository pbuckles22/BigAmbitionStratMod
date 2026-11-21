using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ChaoticCEO
{
    public sealed class BareSight
    {
        public BareSight(int day, BareLeaseFact? lot)
        {
            Day = day;
            Lot = lot;
        }

        public int Day { get; }

        public BareLeaseFact? Lot { get; }
    }

    public static class BareOpen
    {
        public const string Slug = "bare-open";

        private static readonly object Gate = new object();

        private static readonly Dictionary<string, int> Seen = new Dictionary<string, int>(StringComparer.Ordinal);

        private static readonly HashSet<string> Logged = new HashSet<string>(StringComparer.Ordinal);

        private static bool _read;

        private static bool _holding;

        private static int _armedDay;

        public static bool Holding
        {
            get
            {
                lock (Gate)
                {
                    return _holding;
                }
            }
        }

        public static void Reset()
        {
            lock (Gate)
            {
                Seen.Clear();
                Logged.Clear();
                _read = false;
                _holding = false;
                _armedDay = 0;
            }
        }

        public static bool Arm(int day)
        {
            if (day <= 0)
            {
                return false;
            }

            lock (Gate)
            {
                if (day == _armedDay)
                {
                    return false;
                }

                _armedDay = day;
                return true;
            }
        }

        public static string Trip(int morning, IReadOnlyList<BareSight>? sights)
        {
            if (morning <= 0 || sights == null || sights.Count == 0)
            {
                return "";
            }

            var line = "";
            foreach (var sight in sights)
            {
                if (sight == null)
                {
                    continue;
                }

                var next = One(morning, sight.Day, sight.Lot);
                if (next.Length > 0)
                {
                    line = next;
                }
            }

            return line;
        }

        public static BuildingRent Pause(string? trip, BuildingRent? next)
        {
            if (next == null)
            {
                return new BuildingRent("", 0, false, "");
            }

            if (string.IsNullOrEmpty(trip) || trip.IndexOf("slug=" + Slug, StringComparison.Ordinal) < 0)
            {
                return next;
            }

            return new BuildingRent(next.Street, next.Number, false, "");
        }

        public static string Watch(string? localAppData, int day, IReadOnlyList<BareLeaseFact>? lots)
        {
            if (day <= 0)
            {
                return "";
            }

            string winner;
            var append = false;
            lock (Gate)
            {
                Load(localAppData);
                winner = "";
                var winnerKey = "";
                var live = new HashSet<string>(StringComparer.Ordinal);
                if (lots != null)
                {
                    foreach (var lot in lots)
                    {
                        if (lot == null || !BareLease.IsBare(lot))
                        {
                            continue;
                        }

                        var key = Key(lot.Street, lot.Number);
                        live.Add(key);
                        if (!Seen.TryGetValue(key, out var first) || day < first)
                        {
                            Seen[key] = day;
                            Logged.Remove(key);
                            first = day;
                        }

                        var trip = One(day, first, lot);
                        if (trip.Length > 0)
                        {
                            winner = trip;
                            winnerKey = key;
                        }
                    }
                }

                var stale = new List<string>();
                foreach (var key in Seen.Keys)
                {
                    if (!live.Contains(key))
                    {
                        stale.Add(key);
                    }
                }

                foreach (var key in stale)
                {
                    Seen.Remove(key);
                    Logged.Remove(key);
                }

                _holding = winner.Length > 0;
                append = winner.Length > 0 && Logged.Add(winnerKey);
                Save(localAppData);
            }

            if (append && !string.IsNullOrEmpty(localAppData))
            {
                Append(localAppData, winner);
            }

            return winner;
        }

        private static string One(int morning, int firstDay, BareLeaseFact? lot)
        {
            if (lot == null || firstDay <= 0 || morning <= firstDay || !BareLease.IsBare(lot))
            {
                return "";
            }

            return "breaker.tripped version=" + LoadLog.Version
                + " day=" + morning.ToString(CultureInfo.InvariantCulture)
                + " slug=" + Slug
                + " street=" + lot.Street
                + " number=" + lot.Number.ToString(CultureInfo.InvariantCulture);
        }

        private static string Key(string street, int number)
        {
            return street + "\n" + number.ToString(CultureInfo.InvariantCulture);
        }

        private static void Load(string? localAppData)
        {
            if (_read)
            {
                return;
            }

            _read = true;
            if (string.IsNullOrEmpty(localAppData))
            {
                return;
            }

            var path = SeenPath(localAppData);
            if (!File.Exists(path))
            {
                return;
            }

            foreach (var raw in File.ReadAllLines(path))
            {
                var parts = raw.Split('\t');
                if (parts.Length != 3)
                {
                    continue;
                }

                if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                {
                    continue;
                }

                if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var seenDay))
                {
                    continue;
                }

                if (parts[0].Length == 0 || number <= 0 || seenDay <= 0)
                {
                    continue;
                }

                Seen[Key(parts[0], number)] = seenDay;
            }
        }

        private static void Save(string? localAppData)
        {
            if (string.IsNullOrEmpty(localAppData))
            {
                return;
            }

            var path = SeenPath(localAppData);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var text = new StringBuilder();
            foreach (var pair in Seen)
            {
                var split = pair.Key.IndexOf('\n');
                if (split <= 0)
                {
                    continue;
                }

                text.Append(pair.Key.Substring(0, split))
                    .Append('\t')
                    .Append(pair.Key.Substring(split + 1))
                    .Append('\t')
                    .Append(pair.Value.ToString(CultureInfo.InvariantCulture))
                    .Append('\n');
            }

            File.WriteAllText(path, text.ToString());
        }

        private static string SeenPath(string localAppData)
        {
            return Path.Combine(localAppData, LoadLog.ModId, "bare-open.txt");
        }

        private static void Append(string localAppData, string line)
        {
            var path = LoadLog.CeoLogPath(localAppData);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + Environment.NewLine);
        }
    }
}
