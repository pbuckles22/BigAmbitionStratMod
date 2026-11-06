using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class ExecuteButton
    {
        public ExecuteButton(string kind, string street, int number)
        {
            Kind = kind ?? "";
            Street = street ?? "";
            Number = number;
        }

        public string Label
        {
            get { return "Execute"; }
        }

        public string Kind { get; }

        public string Street { get; }

        public int Number { get; }
    }

    public static class Controls
    {
        public const string ExecuteAll = "Execute all";

        public const string ExecuteAllStory = "7.12";

        private static string _seen = "";

        public static IReadOnlyList<ExecuteButton> Execute(IReadOnlyList<PanelLine>? lines)
        {
            var buttons = new List<ExecuteButton>();
            if (lines == null)
            {
                return buttons;
            }

            foreach (var line in lines)
            {
                if (line == null || line.Business != "step" || line.Street.Length == 0 || line.Number <= 0)
                {
                    continue;
                }

                buttons.Add(new ExecuteButton(line.Tone, line.Street, line.Number));
            }

            return buttons;
        }

        public static string Press(ExecuteButton? button)
        {
            if (button == null)
            {
                return "";
            }

            return "action=execute kind=" + button.Kind
                + " street=" + button.Street
                + " number=" + button.Number.ToString(CultureInfo.InvariantCulture);
        }

        public static string PressAll()
        {
            return "action=execute-all story=" + ExecuteAllStory;
        }

        public static string Pressed(string? action)
        {
            return "controls.pressed version=" + LoadLog.Version + " " + (action ?? "");
        }

        public static bool Toggle()
        {
            Autopilot.Enabled = !Autopilot.Enabled;
            return Autopilot.Enabled;
        }

        public static bool Pause()
        {
            Autopilot.Enabled = false;
            return Autopilot.Enabled;
        }

        public static bool Dry()
        {
            Autopilot.DryRun = !Autopilot.DryRun;
            return Autopilot.DryRun;
        }

        public static IReadOnlyList<string> Buttons(bool autopilot, bool dryRun)
        {
            return new[]
            {
                "Autopilot " + (autopilot ? "on" : "off"),
                "Dry run " + (dryRun ? "on" : "off"),
                ExecuteAll,
            };
        }

        public static string Row(bool autopilot, bool dryRun)
        {
            var buttons = Buttons(autopilot, dryRun);
            return buttons[0] + "  " + buttons[1] + "  " + buttons[2];
        }

        public static void Append(string localAppData, string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return;
            }

            var path = LoadLog.CeoLogPath(localAppData ?? "");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + System.Environment.NewLine);
        }

        public static string Record(string localAppData, string line)
        {
            var shown = "controls.shown version=" + LoadLog.Version + " " + (line ?? "");
            var key = (localAppData ?? "") + "\n" + shown;
            if (key == _seen)
            {
                return shown;
            }

            _seen = key;
            Append(localAppData ?? "", shown);
            return shown;
        }
    }
}
