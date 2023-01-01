using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Plugin;

namespace SkillDisplay;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "SkillDisplay";
    public Configuration Configuration;
    private PluginUI PluginUi;

    private delegate void EffectDelegate(uint sourceId, IntPtr sourceCharacter);
    private Hook<EffectDelegate> EffectEffectHook;

    private delegate void ReceiveAbiltyDelegate(uint sourceId, IntPtr sourceCharacter, IntPtr pos,
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
            //took from https://github.com/lmcintyre/DamageInfoPlugin/blob/main/DamageInfoPlugin/DamageInfoPlugin.cs#L133
            ReceivAbilityHook = new Hook<ReceiveAbiltyDelegate>(
                DalamudApi.SigScanner.ScanText("4C 89 44 24 ?? 55 56 41 54 41 55 41 56"),
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
        var type = Marshal.ReadByte(ptr, 2);
        //var data = Marshal.PtrToStructure<ActorCast>(ptr);
        CastHook.Original(source, ptr);
        //if (source == DalamudApi.TargetManager.Target.ObjectId) 
        //    PluginLog.Error($"{data.action_id}:{data.skillType}:{data.unknown}:{data.id}:{data.cast_time}:{data.rotation}:UNKNOWN={data.unknown}:UNKNOWN2={data.unknown_2}:UNKNOWN3={data.unknown_3}:{data.UnkUshort}");
        if (DalamudApi.ClientState.LocalPlayer == null) return;
        if (source != CheckTarget() || type != 1) return;
        //PluginLog.Debug($"Casting:{action}:{type}");
        PluginUi.Cast(source,(uint) action);
    }

    struct ActorCast
    {
        public ushort action_id;
        public byte skillType;
        public byte unknown;
        public uint id; // action id or mount id
        public float cast_time;
        public uint target_id;
        public ushort rotation;
        public ushort flag; // 1 = interruptible blinking cast bar
        public ushort unknown_2;
        public ushort posX;
        public ushort posY;
        public ushort posZ;
        public ushort unknown_3;
        public ushort UnkUshort;
    };

    private void ReceiveActorControlSelf(uint entityId, uint type, uint buffID, uint direct, uint actionId,
        uint sourceId,
        uint arg4, uint arg5, ulong targetId, byte a10)
    {
        ActorControlSelfHook.Original(entityId, type, buffID, direct, actionId, sourceId, arg4, arg5, targetId, a10);
        if (type != 15) return;
        if (DalamudApi.ClientState.LocalPlayer == null) return;
        if (entityId != CheckTarget()) return;
        PluginLog.Debug($"Cancel:{entityId:X} {actionId}");
        PluginUi.Cancel(entityId,actionId);
    }

    private void ReceiveAbilityEffect(uint sourceId, IntPtr sourceCharacter, IntPtr pos, IntPtr effectHeader,
        IntPtr effectArray, IntPtr effectTrail)
    {
        var action = Marshal.ReadInt32(effectHeader, 0x8);
        var type = Marshal.ReadByte(effectHeader, 31);
        ReceivAbilityHook.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTrail);

        if (DalamudApi.ClientState.LocalPlayer == null) return;
        if (sourceId != CheckTarget() || type != 1) return;
        PluginLog.Debug($"Do:{action}:{type}");
        PluginUi.DoAction(sourceId,(uint) action);
    }

    public uint? CheckTarget()
    {
        return Configuration.Mode switch
        {
            1 => DalamudApi.TargetManager.Target?.ObjectId,
            2 => DalamudApi.TargetManager.FocusTarget?.ObjectId,
            _ => DalamudApi.ClientState.LocalPlayer?.ObjectId,
        };
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
    [HelpMessage("Show config window.")]
    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just display our main ui
        PluginUi.SettingsVisible = true;
    }
}