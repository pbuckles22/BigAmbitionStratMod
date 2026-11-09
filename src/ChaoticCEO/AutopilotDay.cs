using System;
using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public static partial class Autopilot
    {
        public static string CallKey(PlaybookStep? step)
        {
            if (step == null || !step.Chosen)
            {
                return "";
            }

            switch (step.Kind)
            {
                case "close":
                    return "close";
                case "open":
                case "where":
                    return "rent";
                case "layout":
                    return "clone";
                case "prices":
                    return "price";
                case "supply":
                    return "delivery";
                case "trucks":
                    return "truck buy";
                case "warehouses":
                    return "purchasing plan";
                case "managers":
                    return "plan assign";
                case "money":
                    return "loan";
                case "realestate":
                    return "buy";
                case "run":
                    if (step.Detail == "sleep")
                    {
                        return "sleep";
                    }

                    if (step.Detail == "eat")
                    {
                        return "food";
                    }

                    return "";
                default:
                    return "";
            }
        }

        public static string Call(PlaybookStep? step, ApiNote? api)
        {
            if (api == null)
            {
                return "";
            }

            var key = CallKey(step);
            return key.Length == 0 ? "" : api.Call(key);
        }

        public static AutopilotDay Day(
            string? localAppData,
            int day,
            ApiNote? api,
            AuditWorld? world,
            AuditGuard? guard,
            Func<AuditHeal, HealFate>? heal,
            PlaybookStep? step,
            PlayerNeeds? needs,
            string? bar,
            string? health,
            Func<TimeSkipPick, bool>? timeBridge = null,
            Func<PlaybookStep, string, bool>? actionBridge = null)
        {
            if (string.IsNullOrWhiteSpace(localAppData) || day <= 0)
            {
                return AutopilotDay.Skipped("");
            }

            if (!Enabled)
            {
                var skipped = Marker(day, ran: false);
                Append(localAppData, skipped);
                return AutopilotDay.Skipped(skipped);
            }

            var marker = Marker(day, ran: true);
            Append(localAppData, marker);

            guard = guard ?? new AuditGuard(Enabled, DryRun, 0m, 0m, 0, LoadLog.PinnedBuild);
            world = world ?? new AuditWorld(
                null, null, null, null, null, null, null, true, null, null, null, null);
            var report = Audit.Check(localAppData, day, api, world, guard, heal);

            var action = "";
            var call = Call(step, api);
            if (step != null && step.Chosen && call.Length > 0)
            {
                var executed = actionBridge == null || actionBridge(step, call);
                if (executed)
                {
                    action = ActionLine(day, step, call);
                    Append(localAppData, action);
                }
            }

            var advanced = "";
            var skip = TimeAdvance.Skip(needs ?? Needs.Hold(), alreadyAdvanced: false, api);
            if (skip.Advanced && (timeBridge == null || timeBridge(skip)))
            {
                advanced = TimeAdvance.RecordSkip(localAppData, skip, day);
            }

            var telemetry = Telemetry.Record(
                localAppData,
                bar,
                health ?? Health.Latest(localAppData),
                Controls.Row(Enabled, DryRun));

            return new AutopilotDay(true, marker, action, advanced, telemetry, report);
        }

        public static string Marker(int day, bool ran)
        {
            return "autopilot.day version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " ran=" + (ran ? "true" : "false");
        }

        public static string ActionLine(int day, PlaybookStep step, string call)
        {
            return "autopilot.action version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " kind=" + step.Kind
                + " detail=" + step.Detail
                + " call=" + call;
        }

        public static string Append(string localAppData, string line)
        {
            if (string.IsNullOrWhiteSpace(localAppData) || string.IsNullOrEmpty(line))
            {
                return "";
            }

            var path = LoadLog.CeoLogPath(localAppData);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + Environment.NewLine);
            return line;
        }
    }
}
