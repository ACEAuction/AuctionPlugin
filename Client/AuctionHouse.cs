using System.IO;
using System;
using Microsoft.Extensions.Logging;
using Chorizite.Core.Plugins;
using Chorizite.Core.Plugins.AssemblyLoader;
using Chorizite.Core;
using RmlUi;
using AC;
using RmlUi.Lib;
using AC.API;

namespace PluginManagerUI {
    public class AuctionHousePlugin : IPluginCore {
        internal static ILogger Log;
        private Panel? _panel;

        public RmlUiPlugin UI { get; }
        public ACPlugin AC { get; }

        protected AuctionHousePlugin(AssemblyPluginManifest manifest, RmlUiPlugin coreUI, ACPlugin coreAC, ILogger log) : base(manifest) {
            Log = log;
            UI = coreUI;
            AC = coreAC;
        }

        protected override void Initialize() {
            if (AC.Game.State == ClientState.InGame) {
                CreatePanel();
            }
            else {
                AC.Game.OnStateChanged += Game_OnStateChanged;
            }
        }

        private void Game_OnStateChanged(object? sender, GameStateChangedEventArgs e) {
            if (e.NewState == ClientState.InGame) {
                AC.Game.OnStateChanged -= Game_OnStateChanged;
                CreatePanel();
            }
        }

        private void CreatePanel() {
            _panel = UI.CreatePanel("ActionHouse", Path.Combine(AssemblyDirectory, "assets", "AuctionHouse.rml"));
            if (_panel is not null) {
                _panel.ShowInBar = true;
                _panel.Show();
            }
        }

        protected override void Dispose() {
            _panel?.Dispose();
        }
    }
}
