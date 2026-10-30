using System;
using System.Collections.Generic;

namespace ChaoticCEO
{
    public sealed class ApiNote
    {
        public ApiNote(string build, string close, string buy, string price, string delivery)
        {
            Build = build ?? "";
            Close = close ?? "";
            Buy = buy ?? "";
            Price = price ?? "";
            Delivery = delivery ?? "";
        }

        public string Build { get; }

        public string Close { get; }

        public string Buy { get; }

        public string Price { get; }

        public string Delivery { get; }
    }

    public static class GameApi
    {
        public static ApiNote Read(IReadOnlyList<PanelLine>? panel, string? note)
        {
            if (panel == null || panel.Count == 0)
            {
                return new ApiNote("", "", "", "", "");
            }

            var build = "";
            var calls = new Dictionary<string, string>();
            foreach (var raw in (note ?? "").Split('\n'))
            {
                var line = raw.Trim().TrimEnd('\r');
                if (line.StartsWith("Build ", StringComparison.Ordinal))
                {
                    build = line.Substring("Build ".Length).Trim().TrimEnd('.');
                }

                if (!line.StartsWith("| ", StringComparison.Ordinal))
                {
                    continue;
                }

                var cells = line.Trim('|').Split('|');
                if (cells.Length != 2)
                {
                    continue;
                }

                var action = cells[0].Trim();
                var call = cells[1].Trim();
                if (action.Length == 0 || action == "action" || action.StartsWith("-", StringComparison.Ordinal))
                {
                    continue;
                }

                calls[action] = call;
            }

            return new ApiNote(
                build,
                Call(calls, "close"),
                Call(calls, "buy"),
                Call(calls, "price"),
                Call(calls, "delivery"));
        }

        private static string Call(Dictionary<string, string> calls, string action)
        {
            if (!calls.TryGetValue(action, out var call) || call == null)
            {
                return "";
            }

            return call;
        }
    }
}
