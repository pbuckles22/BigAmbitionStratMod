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

            LiveDump.HoldNeeds(_needs, _localAppData);
            LiveDump.Roll(_days, _localAppData);
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
    }
}
