using System.Globalization;
using System.IO;

namespace ChaoticCEO
{
    public sealed class PlayerNeeds
    {
        public PlayerNeeds(float energy, float hunger, float happiness, bool energyOff, bool happinessOff)
        {
            Energy = energy;
            Hunger = hunger;
            Happiness = happiness;
            EnergyOff = energyOff;
            HappinessOff = happinessOff;
        }

        public float Energy { get; }

        public float Hunger { get; }

        public float Happiness { get; }

        public bool EnergyOff { get; }

        public bool HappinessOff { get; }
    }

    public static class Needs
    {
        public const float Full = 100f;

        public static PlayerNeeds Hold()
        {
            return new PlayerNeeds(Full, Full, Full, energyOff: true, happinessOff: true);
        }

        public static bool Due(PlayerNeeds now)
        {
            return now.Energy < Full
                || now.Hunger < Full
                || now.Happiness < Full
                || !now.EnergyOff
                || !now.HappinessOff;
        }

        public static string Line(int day, PlayerNeeds before)
        {
            return "needs.held version=" + LoadLog.Version
                + " day=" + day.ToString(CultureInfo.InvariantCulture)
                + " energy=" + Number(before.Energy)
                + " hunger=" + Number(before.Hunger)
                + " happiness=" + Number(before.Happiness)
                + " energyOff=" + Flag(before.EnergyOff)
                + " happinessOff=" + Flag(before.HappinessOff);
        }

        private static string Number(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string Flag(bool value)
        {
            return value ? "true" : "false";
        }
    }

    public sealed class NeedsWatch
    {
        private object? _held;

        public PlayerNeeds? Tick(string localAppData, object? save, int day, PlayerNeeds now)
        {
            if (save == null)
            {
                return null;
            }

            if (!ReferenceEquals(_held, save))
            {
                var path = LoadLog.CeoLogPath(localAppData);
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(path, Needs.Line(day, now) + System.Environment.NewLine);
                _held = save;
            }

            return Needs.Due(now) ? Needs.Hold() : null;
        }
    }
}
