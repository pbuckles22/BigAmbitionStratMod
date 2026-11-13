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
            LoadLog.StartSession(localAppData);
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
            DumpOnHotkey();
            if (!_panelFailed)
            {
                try
                {
                    PanelView.Ensure(_localAppData);
                }
                catch (Exception ex)
                {
                    _panelFailed = true;
                    File.AppendAllText(
                        LoadLog.CeoLogPath(_localAppData),
                        "panel.failed " + ex.GetType().Name + " " + ex.Message + Environment.NewLine);
                }
            }

            StopOnHotkey();
            ResumeOnHotkey();
            if (!FrameTick.Due(Time.unscaledTime, _nextTickTime))
            {
                return;
            }

            _nextTickTime = FrameTick.Next(Time.unscaledTime);
            LiveDump.HoldNeeds(_needs, _localAppData);
            LiveDump.TryRent(_rents, _localAppData);
            LiveDump.TryLayout(_layouts, _localAppData);
            LiveDump.TryOps(_ops, _localAppData);
            LiveDump.TryCapacity(_capacity, _localAppData);
            LiveDump.TryWholesaler(_contracts, _localAppData);
            LiveDump.TryCashier(_cashiers, _localAppData);
            LiveDump.TryRestaff(_cashiers, _localAppData);
            LiveDump.ReleaseCleaner(_cleaners, _mornings);
            LiveDump.TryCleaner(_cleaners, _localAppData);
            LiveDump.TryCover(_mornings, _localAppData);
            LiveDump.TryLoan(_loans, _localAppData);
            LiveDump.TryDemand(_demands, _localAppData);
            LiveDump.TryTax(_taxes, _localAppData);
            LiveDump.TryTime(_time, _localAppData);
            LiveDump.TryMarketing(_marketing, _time, _localAppData);
            LiveDump.TryTakeover(_marketing, _takeovers, _localAppData);
            if (LiveDump.Roll(_days, _localAppData) != null)
            {
                LiveDump.FillSlots(_slots, _localAppData);
                LiveDump.ConnectStores(_stores, _localAppData);
                LiveDump.AssignPlans(_plans, _localAppData);
                LiveDump.RestockWarehouses(_restock, _localAppData);
                // Fit-out once per day roll — never per frame (scene scans stall the clock).
                LiveDump.TryFitOut(_localAppData);
                if (Autopilot.Enabled)
                {
                    LiveDump.RunAutopilot(_localAppData);
                }
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
