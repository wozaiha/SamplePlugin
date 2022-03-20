using System;


namespace SamplePlugin
{
    internal class PluginUI : IDisposable
    {
        private Configuration config;

        public bool Visible = false;
        public bool SettingsVisible = false;
        private Plugin _plugin;


        public PluginUI(Plugin p)
        {
            _plugin = p;
            config = p.Configuration;
            DalamudApi.PluginInterface.UiBuilder.Draw += Draw;
            DalamudApi.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        }

        public void Dispose()
        {
            DalamudApi.PluginInterface.UiBuilder.Draw -= Draw;
            DalamudApi.PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;

        }


        public void DrawConfigUI()
        {
            Visible = true;
        }

        public void Draw()
        {

        }

        public void DrawConfig()
        {

        }


        public void DrawACT()
        {

        }


        public void OnBuildUi_Debug()
        {
        }
    }
}
