using System;
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
        private bool _hooked;
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
            TogglePanel();
            LiveDump.Roll(_days, _localAppData);
        }

        private void TogglePanel()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.f9Key.wasPressedThisFrame)
            {
                return;
            }

            PanelView.Ensure().Press(_localAppData);
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
