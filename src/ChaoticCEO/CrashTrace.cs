using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ChaoticCEO
{
    public static class CrashTrace
    {
        public const int TrailLimit = 24;

        private static readonly object Gate = new object();
        private static readonly List<string> Steps = new List<string>();
        private static readonly HashSet<string> SeenKeys = new HashSet<string>(StringComparer.Ordinal);
        private static string _path = "";
        private static int _day = -1;
        private static bool _armed;

        public static void Reset()
        {
            lock (Gate)
            {
                Steps.Clear();
                SeenKeys.Clear();
                _path = "";
                _day = -1;
                _armed = false;
            }
        }

        public static void Bind(string localAppData)
        {
            lock (Gate)
            {
                _path = string.IsNullOrEmpty(localAppData) ? "" : LoadLog.CeoLogPath(localAppData);
            }
        }

        public static string Arm(string localAppData)
        {
            lock (Gate)
            {
                if (!string.IsNullOrEmpty(localAppData))
                {
                    _path = LoadLog.CeoLogPath(localAppData);
                }

                if (_armed || string.IsNullOrEmpty(_path))
                {
                    return "";
                }

                _armed = true;
                var line = "trace.armed version=" + LoadLog.Version;
                Append(_path, line);
                return line;
            }
        }

        public static string Pulse(int day)
        {
            lock (Gate)
            {
                if (day != _day)
                {
                    _day = day;
                    SeenKeys.Clear();
                }

                return "day=" + day.ToString(CultureInfo.InvariantCulture);
            }
        }

        public static void Begin()
        {
            lock (Gate)
            {
                Steps.Clear();
            }
        }

        public static void Note(string step, string payload)
        {
            lock (Gate)
            {
                NoteUnlocked(step, payload);
            }
        }

        public static string Trail()
        {
            lock (Gate)
            {
                return TrailUnlocked();
            }
        }

        public static bool Guard(string where, string payload, Action body)
        {
            lock (Gate)
            {
                NoteUnlocked(where, payload);
            }

            try
            {
                body?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                lock (Gate)
                {
                    WriteUnlocked(_path, ex, where, payload, note: false);
                }

                return false;
            }
        }

        public static string Catch(string path, Exception ex, string where, string payload)
        {
            if (ex == null || string.IsNullOrEmpty(path))
            {
                return "";
            }

            lock (Gate)
            {
                return WriteUnlocked(path, ex, where, payload, note: true);
            }
        }

        public static string Seen(Exception ex, string where, string payload)
        {
            if (ex == null)
            {
                return "";
            }

            lock (Gate)
            {
                return WriteUnlocked(_path, ex, where, payload ?? "", note: true);
            }
        }

        public static string WinLine(int day, decimal netWorth)
        {
            lock (Gate)
            {
                return "win.trail version=" + LoadLog.Version
                    + " day=" + day.ToString(CultureInfo.InvariantCulture)
                    + " netWorth=" + netWorth.ToString(CultureInfo.InvariantCulture)
                    + " trail=" + TrailUnlocked();
            }
        }

        private static string WriteUnlocked(string path, Exception ex, string where, string payload, bool note)
        {
            var key = Flatten(where) + "|" + ex.GetType().Name + "|" + Flatten(ex.Message ?? "");
            if (!SeenKeys.Add(key))
            {
                return "";
            }

            if (note)
            {
                NoteUnlocked(where, payload);
            }

            var line = "crash.caught version=" + LoadLog.Version
                + " where=" + Flatten(where)
                + " type=" + ex.GetType().Name
                + " slug=UNHANDLED"
                + " day=" + (_day < 0 ? "0" : _day.ToString(CultureInfo.InvariantCulture))
                + " payload=" + Flatten(payload)
                + " trail=" + TrailUnlocked()
                + " message=" + Flatten(ex.Message ?? "")
                + " stack=" + Flatten(ex.StackTrace ?? "");
            if (!string.IsNullOrEmpty(path))
            {
                Append(path, line);
            }

            return line;
        }

        private static void NoteUnlocked(string step, string payload)
        {
            if (string.IsNullOrEmpty(step))
            {
                return;
            }

            var token = Flatten(step);
            if (!string.IsNullOrEmpty(payload))
            {
                token += "{" + Flatten(payload) + "}";
            }

            Steps.Add(token);
            while (Steps.Count > TrailLimit)
            {
                Steps.RemoveAt(0);
            }
        }

        private static string TrailUnlocked()
        {
            return Steps.Count == 0 ? "-" : string.Join(">", Steps);
        }

        private static string Flatten(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "-";
            }

            var text = new StringBuilder(value.Length);
            var previous = false;
            foreach (var c in value)
            {
                var keep = (c >= 'a' && c <= 'z')
                    || (c >= 'A' && c <= 'Z')
                    || (c >= '0' && c <= '9')
                    || c == '='
                    || c == '.'
                    || c == '_'
                    || c == ':'
                    || c == '-';
                if (keep)
                {
                    text.Append(c);
                    previous = false;
                    continue;
                }

                if (!previous)
                {
                    text.Append('_');
                    previous = true;
                }
            }

            var flat = text.ToString().Trim('_');
            if (flat.Length == 0)
            {
                return "-";
            }

            return flat.Length > 800 ? flat.Substring(0, 800) : flat;
        }

        private static void Append(string path, string line)
        {
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(path, line + Environment.NewLine);
            }
            catch (Exception)
            {
            }
        }
    }
}
