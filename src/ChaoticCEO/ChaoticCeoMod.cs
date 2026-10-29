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
        private bool _hooked;
        private string _localAppData = "";

        public string[] RelativeAssetBundlePaths => Array.Empty<string>();

        public Task OnLoadAsync(ModContext context)
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            LoadLog.RecordPinned(localAppData);
            _localAppData = localAppData;
            if (!_hooked)
            {
                UnityLifecycleProvider.OnUpdate += DumpOnHotkey;
                _hooked = true;
            }

            return Task.CompletedTask;
        }

        public Task OnUnloadAsync()
        {
            if (_hooked)
            {
                UnityLifecycleProvider.OnUpdate -= DumpOnHotkey;
                _hooked = false;
            }

            return Task.CompletedTask;
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
