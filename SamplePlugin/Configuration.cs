using System;
using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace SkillDisplay
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;
        public float IconSize = 48f;
        public Vector4 color= Vector4.One;
        public bool ShowAuto = false;
        public float Alpha = 0.7f;
        public bool Lock = false;


        // the below exist just to make saving less cumbersome

        [NonSerialized]
        private DalamudPluginInterface? pluginInterface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.pluginInterface!.SavePluginConfig(this);
        }
    }
}
