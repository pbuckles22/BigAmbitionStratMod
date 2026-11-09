using System;
using System.IO;
using System.Threading.Tasks;
using BAModAPI;
using BAModAPI.Services;
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
        private readonly BankLoanWatch _loans = new BankLoanWatch();
        private readonly StaffDemandWatch _demands = new StaffDemandWatch();
        private readonly TaxBillWatch _taxes = new TaxBillWatch();
        private readonly TimeAdvanceWatch _time = new TimeAdvanceWatch();
        private readonly MarketingWatch _marketing = new MarketingWatch();
        private readonly RivalTakeoverWatch _takeovers = new RivalTakeoverWatch();
        private bool _hooked;
        private bool _panelFailed;
        private string _localAppData = "";

        public string[] RelativeAssetBundlePaths => Array.Empty<string>();

        public Task OnLoadAsync(ModContext context)
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            LoadLog.StartSession(localAppData);
            _localAppData = localAppData;
            if (!_hooked)
            {
                UnityLifecycleProvider.OnUpdate += OnFrame;
                _hooked = true;
            }

            return Task.CompletedTask;
        }

        public Task OnUnloadAsync()
        {
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
            LiveDump.HoldNeeds(_needs, _localAppData);
            LiveDump.TryRent(_rents, _localAppData);
            LiveDump.TryLayout(_layouts, _localAppData);
            LiveDump.TryOps(_ops, _localAppData);
            LiveDump.TryCapacity(_capacity, _localAppData);
            LiveDump.TryWholesaler(_contracts, _localAppData);
            LiveDump.TryCashier(_cashiers, _localAppData);
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

        private void StopOnHotkey()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.f10Key.wasPressedThisFrame || !Autopilot.Enabled)
            {
                return;
            }

            Controls.Pause();
            File.AppendAllText(
                LoadLog.CeoLogPath(_localAppData),
                "autopilot.paused version=" + LoadLog.Version + " reason=f10" + Environment.NewLine);
        }
    }
}
