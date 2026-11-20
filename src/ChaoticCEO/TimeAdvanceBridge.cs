using System.Reflection;
using Timemachine;
using UnityEngine;

namespace ChaoticCEO
{
    public static class TimeAdvanceBridge
    {
        private const int CurveSamples = 64;

        private static readonly SleepSkip Gate = new SleepSkip();
        private static readonly FieldInfo? DistanceField =
            typeof(TimeMachine).GetField("_timeDistance", BindingFlags.Instance | BindingFlags.NonPublic);

        private static TimeMachine? _machine;
        private static GameSpeedController? _clock;

        public static bool DesiredOn => Gate.DesiredOn;

        public static bool Defer(TimeSkipPick pick)
        {
            _ = pick;
            return false;
        }

        public static bool Skipping()
        {
            return Gate.Started && Running();
        }

        public static bool Toggle(string localAppData, string reason = "f6")
        {
            var on = Gate.Toggle();
            if (!on)
            {
                var current = SaveGameManager.Current;
                Cancel();
                TimeAdvance.RecordSynced(localAppData, current == null ? 0 : current.Day, "stop", reason);
            }

            return on;
        }

        public static string Sync(string localAppData, bool allowed, string reason = "frame")
        {
            var current = SaveGameManager.Current;
            var day = current == null ? 0 : current.Day;
            var action = Gate.Sync(SceneReady(current), current, allowed, Running());
            if (action == "start")
            {
                var rate = Start();
                if (rate <= 0f)
                {
                    Gate.Release();
                    return "hold";
                }

                TimeAdvance.RecordSynced(localAppData, day, "start", reason, rate);
            }
            else if (action == "ended")
            {
                ReleaseClock();
                TimeAdvance.RecordSynced(localAppData, day, "stop", "esc");
            }

            return action;
        }

        public static bool Resume(string localAppData)
        {
            _ = localAppData;
            Gate.Resume();
            return Gate.DesiredOn;
        }

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
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "TimeAdvanceBridge.Care.1", "");
                return false;
            }
        }

        public static bool Stop()
        {
            Gate.Stop();
            return Cancel();
        }

        private static float Start()
        {
            try
            {
                var machine = Machine();
                if (machine == null)
                {
                    return 0f;
                }

                var rate = TimeAdvance.SleepRate(Sample(machine.timeSpeedCurve));
                TimeMachine.SkipTime(TimeAdvance.SoakSpan, TimeAdvance.ConstantSkip);
                if (!machine.isRunning)
                {
                    return 0f;
                }

                // Constant speed is _timeDistance minutes per second. Pin it to sleep's speed.
                DistanceField?.SetValue(machine, rate);
                return rate;
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "TimeAdvanceBridge.Start.1", "");
                return 0f;
            }
        }

        private static float[] Sample(AnimationCurve? curve)
        {
            if (curve == null || curve.length == 0)
            {
                return System.Array.Empty<float>();
            }

            var samples = new float[CurveSamples + 1];
            for (var i = 0; i <= CurveSamples; i++)
            {
                samples[i] = curve.Evaluate(i / (float)CurveSamples);
            }

            return samples;
        }

        private static bool Running()
        {
            try
            {
                var machine = Machine();
                return machine != null && machine.isRunning;
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "TimeAdvanceBridge.Running.1", "");
                return false;
            }
        }

        private static bool Cancel()
        {
            var cancelled = false;
            try
            {
                var machine = Machine();
                if (machine != null && machine.isRunning)
                {
                    machine.CancelTimeMachine();
                    cancelled = true;
                }
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "TimeAdvanceBridge.Cancel.1", "");
            }

            ReleaseClock();
            return cancelled;
        }

        private static bool SceneReady(object? current)
        {
            if (current == null)
            {
                return false;
            }

            try
            {
                var screenUp = InstanceBehavior<UI.Load.LoadingScreen>.Instance != null;
                return TimeAdvance.CityReady(UI.Load.LoadScene.isLoading, screenUp);
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "TimeAdvanceBridge.SceneReady.1", "");
                return false;
            }
        }

        private static TimeMachine? Machine()
        {
            if (_machine == null)
            {
                var uis = InstanceBehavior<UI.UIs>.Instance;
                _machine = uis == null ? null : uis.timeMachine;
            }

            return _machine;
        }

        private static GameSpeedController? Clock()
        {
            if (_clock == null)
            {
                _clock = Object.FindObjectOfType<GameSpeedController>();
            }

            return _clock;
        }

        private static void ReleaseClock()
        {
            try
            {
                var clock = Clock();
                if (clock == null)
                {
                    return;
                }

                clock.isFastForwarding = false;
                clock.isTimeControlDisabled = false;
                clock.DisableTimeControl(false);
                if (clock.Paused)
                {
                    clock.SetPlayerPause(false, false);
                }

                clock.Set(new GameSpeed(false, TimeSpeed.Normal, false));
            }
            catch (System.Exception ex)
            {
                CrashTrace.Seen(ex, "TimeAdvanceBridge.ReleaseClock.1", "");
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
                catch (System.Exception ex)
                {
                    CrashTrace.Seen(ex, "TimeAdvanceBridge.SleepBed.1", "");
                }
            }
        }
    }
}
