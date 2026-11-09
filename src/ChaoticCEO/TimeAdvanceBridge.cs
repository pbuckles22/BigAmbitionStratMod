using Timemachine;
using UnityEngine;

namespace ChaoticCEO
{
    public static class TimeAdvanceBridge
    {
        public static PlayerNeeds Read()
        {
            var current = SaveGameManager.Current;
            if (current?.gameVariables == null)
            {
                return Needs.Hold();
            }

            return new PlayerNeeds(
                current.Energy,
                current.Hunger,
                current.Happiness,
                current.gameVariables.disableEnergy,
                current.gameVariables.disableHappiness);
        }

        public static bool Care(TimeCarePick? pick)
        {
            if (pick == null || !pick.Due)
            {
                return false;
            }

            var current = SaveGameManager.Current;
            if (current == null)
            {
                return false;
            }

            try
            {
                if (pick.Slept)
                {
                    SleepBed();
                    current.Energy = Needs.Full;
                }

                if (pick.Ate)
                {
                    current.Hunger = Needs.Full;
                }

                if (pick.Slept && pick.Ate)
                {
                    current.Happiness = Needs.Full;
                }

                SaveGameManager.MarkChange();
                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        public static bool Skip(TimeSkipPick? pick)
        {
            if (pick == null || !pick.Advanced || pick.Span.Length == 0)
            {
                return false;
            }

            try
            {
                // FastForward before SkipTime — the (span, useConstantSpeed) overload stalled soak.
                BoostForSkip();
                TimeMachine.SkipTime(pick.Span);
                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        public static bool CancelRunning()
        {
            try
            {
                var machine = Object.FindObjectOfType<TimeMachine>();
                if (machine == null || !machine.isRunning)
                {
                    return false;
                }

                machine.CancelTimeMachine();
                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        public static bool BoostForSkip()
        {
            try
            {
                var clock = Object.FindObjectOfType<GameSpeedController>();
                if (clock == null)
                {
                    return false;
                }

                if (clock.Paused)
                {
                    clock.SetPlayerPause(false, false);
                }

                clock.Set(new GameSpeed(false, TimeSpeed.FastForward, false));
                return !clock.Paused;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private static void SleepBed()
        {
            var beds = Object.FindObjectsOfType<BedController>();
            if (beds == null || beds.Length == 0)
            {
                return;
            }

            foreach (var bed in beds)
            {
                if (bed == null)
                {
                    continue;
                }

                try
                {
                    bed.PerformActivity();
                    return;
                }
                catch (System.Exception)
                {
                }
            }
        }
    }
}
