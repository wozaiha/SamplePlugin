using System;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Plugin;

namespace SkillDisplay
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "SkillDisplay";
        public Configuration Configuration;
        private PluginUI PluginUi;

        private delegate void EffectDelegate(uint sourceId, IntPtr sourceCharacter);
        private Hook<EffectDelegate> EffectEffectHook;

        private delegate void ReceiveAbiltyDelegate(int sourceId, IntPtr sourceCharacter, IntPtr pos,
            IntPtr effectHeader, IntPtr effectArray, IntPtr effectTrail);
        private Hook<ReceiveAbiltyDelegate> ReceivAbilityHook;

        private delegate void ActorControlSelfDelegate(uint entityId, uint id, uint arg0, uint arg1, uint arg2,
            uint arg3, uint arg4, uint arg5, ulong targetId, byte a10);
        private Hook<ActorControlSelfDelegate> ActorControlSelfHook;

        private delegate void CastDelegate(uint sourceId, IntPtr sourceCharacter);
        private Hook<CastDelegate> CastHook;

        public Plugin(DalamudPluginInterface pluginInterface)
        {
            DalamudApi.Initialize(this, pluginInterface);

            Configuration = DalamudApi.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(DalamudApi.PluginInterface);

            PluginUi = new PluginUI(this);

            #region Hook

            {
                ReceivAbilityHook = new Hook<ReceiveAbiltyDelegate>(
                    DalamudApi.SigScanner.ScanText("4C 89 44 24 18 53 56 57 41 54 41 57 48 81 EC ?? 00 00 00 8B F9"),
                    ReceiveAbilityEffect);
                ReceivAbilityHook.Enable();
                ActorControlSelfHook = new Hook<ActorControlSelfDelegate>(
                    DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 0F B7 0B 83 E9 64"), ReceiveActorControlSelf);
                ActorControlSelfHook.Enable();
                CastHook = new Hook<CastDelegate>(
                    DalamudApi.SigScanner.ScanText("40 55 56 48 81 EC ?? ?? ?? ?? 48 8B EA"), StartCast);
                CastHook.Enable();
            }

            #endregion
        }

        private void StartCast(uint source, IntPtr ptr)
        {
            var action = Marshal.ReadInt16(ptr);
            
            CastHook.Original(source, ptr);
            PluginLog.Information($"Casting:{action}");
            if (source == DalamudApi.ClientState.LocalPlayer?.ObjectId) PluginUi.Cast((uint)action);
        }

        private void ReceiveActorControlSelf(uint entityId, uint type, uint buffID, uint direct, uint actionId, uint sourceId,
            uint arg4, uint arg5, ulong targetId, byte a10)
        {
            ActorControlSelfHook.Original(entityId, type, buffID, direct, actionId, sourceId, arg4, arg5, targetId, a10);
            if (type == 15) PluginLog.Log($"Cancel:{entityId:X} {actionId}");
            if (type != 15) return;
            if (entityId == DalamudApi.ClientState.LocalPlayer?.ObjectId)  PluginUi.Cancel((uint)actionId);
        }

        private unsafe void ReceiveAbilityEffect(int sourceId, IntPtr sourceCharacter, IntPtr pos, IntPtr effectHeader,
            IntPtr effectArray, IntPtr effectTrail)
        {
            var action = Marshal.ReadInt32(effectHeader + 0x8);
            ReceivAbilityHook.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTrail);
            PluginLog.Log($"Do:{action}");
            if (sourceId == DalamudApi.ClientState.LocalPlayer?.ObjectId)  PluginUi.DoAction((uint)action);
        }
        
        public void Dispose()
        {
            EffectEffectHook?.Dispose();
            CastHook.Dispose();
            ReceivAbilityHook.Dispose();
            ActorControlSelfHook.Dispose();
            PluginUi?.Dispose();
            DalamudApi.Dispose();
        }

        [Command("/skilldisplay")]
        [HelpMessage("显示设置窗口.")]
        private void OnCommand(string command, string args)
        {
            // in response to the slash command, just display our main ui
            PluginUi.SettingsVisible = true;
        }

        [StructLayout(LayoutKind.Explicit, Size = 0x2A)]
        public struct Header
        {
            [FieldOffset(0x0)] private ulong animationTargetId; // who the animation targets

            [FieldOffset(0x8)] public uint actionId; // what the casting player casts, shown in battle log/ui
            [FieldOffset(0xC)] private uint globalSequence; // seems to only increment on retail?

            [FieldOffset(0x10)] private float animationLockTime; // maybe? doesn't seem to do anything

            [FieldOffset(0x14)]
            private uint someTargetId; // always 00 00 00 E0, 0x0E000000 is the internal def for INVALID TARGET ID

            [FieldOffset(0x18)]
            private ushort
                sourceSequence; // if 0, always shows animation, otherwise hides it. counts up by 1 for each animation skipped on a caster

            [FieldOffset(0x1A)] private ushort rotation;
            [FieldOffset(0x1C)] private ushort actionAnimationId; // the animation that is played by the casting character
            [FieldOffset(0x1E)] private byte variation; // variation in the animation
            [FieldOffset(0x1F)] private byte effectDisplayType;

            [FieldOffset(0x20)]
            private byte unknown20; // is read by handler, runs code which gets the LODWORD of animationLockTime (wtf?)

            [FieldOffset(0x21)] private byte effectCount; // ignores effects if 0, otherwise parses all of them
            [FieldOffset(0x22)] private ushort padding0;

            [FieldOffset(0x24)] private uint padding1;
            [FieldOffset(0x28)] private ushort padding2;
        }
    }
}
