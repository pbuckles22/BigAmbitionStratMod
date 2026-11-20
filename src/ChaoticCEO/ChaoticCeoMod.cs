using System;
using System.IO;
using System.Threading.Tasks;
using BAModAPI;
using BAModAPI.Services;
using UnityEngine;
using UnityEngine.InputSystem;

[assembly: RegisterModClass(typeof(ChaoticCEO.ChaoticCeoMod))]

namespace ChaoticCEO
{
    [ModEntryOnInitializationLoad]
    public class ChaoticCeoMod : IModBigAmbitions
    {
        private readonly DayWatch _days = new DayWatch();
        private readonly NeedsWatch _needs = new NeedsWatch();
        private readonly StaffHoldWatch _staff = new StaffHoldWatch();
        private readonly SlotWatch _slots = new SlotWatch();
        private readonly ConnectWatch _stores = new ConnectWatch();
        private readonly PlanWatch _plans = new PlanWatch();
        private readonly RestockWatch _restock = new RestockWatch();
        private readonly RentWatch _rents = new RentWatch();
        private readonly StarterWatch _layouts = new StarterWatch();
        private readonly StoreOpsWatch _ops = new StoreOpsWatch();
        private readonly CapacityWatch _capacity = new CapacityWatch();
        private readonly WholesalerWatch _contracts = new WholesalerWatch();
        private readonly CashierWatch _cashiers = new CashierWatch();
        private readonly CashierWatch _cleaners = new CashierWatch();
        private readonly HeadcountWatch _headcount = new HeadcountWatch();
        private readonly MorningWatch _mornings = new MorningWatch();
        private readonly BankLoanWatch _loans = new BankLoanWatch();
        private readonly StaffDemandWatch _demands = new StaffDemandWatch();
        private readonly TaxBillWatch _taxes = new TaxBillWatch();
        private readonly TimeAdvanceWatch _time = new TimeAdvanceWatch();
        private readonly MarketingWatch _marketing = new MarketingWatch();
        private readonly RivalTakeoverWatch _takeovers = new RivalTakeoverWatch();
        private bool _hooked;
        private bool _panelFailed;
        private string _localAppData = "";
        private float _nextTickTime;

        public string[] RelativeAssetBundlePaths => Array.Empty<string>();

        public Task OnLoadAsync(ModContext context)
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            CrashTrace.Reset();
            SpendDump.Reset();
            LoadLog.StartSession(localAppData);
            CrashTrace.Bind(localAppData);
            _localAppData = localAppData;
            Controls.OnResumeSkip = () =>
            {
                TimeAdvanceBridge.Resume(_localAppData);
                Autopilot.Enabled = true;
                _time.ClearWait();
            };
            Controls.MeasureBoost = () => false;
            if (!_hooked)
            {
                UnityLifecycleProvider.OnUpdate += OnFrame;
                _hooked = true;
            }

            return Task.CompletedTask;
        }

        public Task OnUnloadAsync()
        {
            Controls.OnResumeSkip = null;
            Controls.MeasureBoost = null;
            if (_hooked)
            {
                UnityLifecycleProvider.OnUpdate -= OnFrame;
                _hooked = false;
            }

            PanelView.Release();
            return Task.CompletedTask;
        }

        private void OnFrame()
        {
            CrashTrace.Arm(_localAppData);
            var pulse = Pulse();
            CrashTrace.Begin();
            CrashTrace.Guard("Dump", pulse, DumpOnHotkey);
            if (!_panelFailed)
            {
                try
                {
                    PanelView.Ensure(_localAppData);
                    CrashTrace.Note("Panel", pulse);
                }
                catch (Exception ex)
                {
                    _panelFailed = true;
                    CrashTrace.Seen(ex, "Panel", pulse);
                }
            }

            CrashTrace.Guard("Stop", pulse, StopOnHotkey);
            CrashTrace.Guard("Resume", pulse, ResumeOnHotkey);
            CrashTrace.Guard("Escape", pulse, StopOnEscape);
            if (!FrameTick.Due(Time.unscaledTime, _nextTickTime))
            {
                return;
            }

            _nextTickTime = FrameTick.Next(Time.unscaledTime);
            CrashTrace.Guard("BeginGame", pulse, () => LiveDump.BeginGame(_localAppData));
            CrashTrace.Guard("HoldNeeds", pulse, () => LiveDump.HoldNeeds(_needs, _localAppData));
            CrashTrace.Guard("StaffHold", pulse, () => StaffHoldBridge.Hold(_staff, _localAppData));
            CrashTrace.Guard("TryRent", pulse, () => LiveDump.TryRent(_rents, _localAppData));
            CrashTrace.Guard("TryLayout", pulse, () => LiveDump.TryLayout(_layouts, _localAppData));
            CrashTrace.Guard("TryOps", pulse, () => LiveDump.TryOps(_ops, _localAppData));
            CrashTrace.Guard("TryCapacity", pulse, () => LiveDump.TryCapacity(_capacity, _localAppData));
            CrashTrace.Guard("TryWholesaler", pulse, () => LiveDump.TryWholesaler(_contracts, _localAppData));
            CrashTrace.Guard("TryCashier", pulse, () => LiveDump.TryCashier(_cashiers, _localAppData));
            CrashTrace.Guard("Headcount", pulse, () => HeadcountBridge.Hold(_headcount, _localAppData));
            CrashTrace.Guard("TryRestaff", pulse, () => LiveDump.TryRestaff(_cashiers, _localAppData));
            CrashTrace.Guard("ReleaseCleaner", pulse, () => LiveDump.ReleaseCleaner(_cleaners, _mornings));
            CrashTrace.Guard("TryCleaner", pulse, () => LiveDump.TryCleaner(_cleaners, _localAppData));
            CrashTrace.Guard("TrySurvey", pulse, () => LiveDump.TrySurvey(_localAppData));
            CrashTrace.Guard("TryCover", pulse, () => LiveDump.TryCover(_mornings, _localAppData));
            CrashTrace.Guard("TryLoan", pulse, () => LiveDump.TryLoan(_loans, _localAppData));
            CrashTrace.Guard("TryDemand", pulse, () => LiveDump.TryDemand(_demands, _localAppData));
            CrashTrace.Guard("TryTax", pulse, () => LiveDump.TryTax(_taxes, _localAppData));
            CrashTrace.Guard("TryTime", pulse, () => LiveDump.TryTime(_time, _localAppData));
            CrashTrace.Guard("TryMarketing", pulse, () => LiveDump.TryMarketing(_marketing, _time, _localAppData));
            CrashTrace.Guard("TryTakeover", pulse, () => LiveDump.TryTakeover(_marketing, _takeovers, _localAppData));
            string? rolled = null;
            CrashTrace.Guard("Roll", pulse, () => rolled = LiveDump.Roll(_days, _localAppData));
            if (rolled != null)
            {
                CrashTrace.Guard("FillSlots", pulse, () => LiveDump.FillSlots(_slots, _localAppData));
                CrashTrace.Guard("ConnectStores", pulse, () => LiveDump.ConnectStores(_stores, _localAppData));
                CrashTrace.Guard("AssignPlans", pulse, () => LiveDump.AssignPlans(_plans, _localAppData));
                CrashTrace.Guard("Restock", pulse, () => LiveDump.RestockWarehouses(_restock, _localAppData));
                // Fit-out once per day roll — never per frame (scene scans stall the clock).
                CrashTrace.Guard("TryFitOut", pulse, () => LiveDump.TryFitOut(_localAppData));
                if (Autopilot.Enabled && !TimeAdvance.LeaveSkipAlone(TimeAdvanceBridge.Skipping()))
                {
                    CrashTrace.Guard("RunAutopilot", pulse, () => LiveDump.RunAutopilot(_localAppData));
                }
            }
        }

        private static string Pulse()
        {
            try
            {
                var current = SaveGameManager.Current;
                return CrashTrace.Pulse(current == null ? 0 : current.Day);
            }
            catch (Exception)
            {
                return CrashTrace.Pulse(0);
            }
        }

        private void DumpOnHotkey()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.f8Key.wasPressedThisFrame)
            {
                return;
            }

            LiveDump.Dump(_localAppData);
        }

        private void ResumeOnHotkey()
        {
            var keyboard = Keyboard.current;
            var pressed = keyboard != null && keyboard.f6Key.wasPressedThisFrame;
            if (!pressed && !Input.GetKeyDown(KeyCode.F6))
            {
                return;
            }

            var on = TimeAdvanceBridge.Toggle(_localAppData);
            if (on)
            {
                Autopilot.Enabled = true;
            }
            else
            {
                Controls.Pause();
                _time.ClearWait();
            }

            File.AppendAllText(
                LoadLog.CeoLogPath(_localAppData),
                "controls.pressed version=" + LoadLog.Version
                    + " action=toggle-skip key=f6 on=" + (on ? "true" : "false") + Environment.NewLine);
        }

        private void StopOnEscape()
        {
            var keyboard = Keyboard.current;
            var pressed = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            if (!pressed && !Input.GetKeyDown(KeyCode.Escape))
            {
                return;
            }

            if (!TimeAdvance.EscStops(true, TimeAdvanceBridge.Skipping()))
            {
                return;
            }

            TimeAdvanceBridge.Stop();
            Controls.Pause();
            _time.ClearWait();
            var current = SaveGameManager.Current;
            TimeAdvance.RecordSynced(_localAppData, current == null ? 0 : current.Day, "stop", "esc");
        }

        private void StopOnHotkey()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.f10Key.wasPressedThisFrame || !Autopilot.Enabled)
            {
                return;
            }

            Controls.Pause();
            if (TimeAdvanceBridge.DesiredOn)
            {
                TimeAdvanceBridge.Toggle(_localAppData, "f10");
            }
            else
            {
                TimeAdvanceBridge.Stop();
            }

            File.AppendAllText(
                LoadLog.CeoLogPath(_localAppData),
                "autopilot.paused version=" + LoadLog.Version + " reason=f10" + Environment.NewLine);
        }
    }
}
