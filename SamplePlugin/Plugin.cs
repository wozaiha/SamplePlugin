using System;
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
                DalamudApi.SigScanner.ScanText(
                    DalamudApi.DataManager.GameData.Repositories["ffxiv"].Version == "2022.04.15.0000.0000"
                        ? "4C 89 44 24 18 53 56 57 41 54 41 57 48 81 EC ?? 00 00 00 8B F9"
                        : "4C 89 44 24 ?? 55 56 57 41 54 41 55 41 56 48 8D 6C 24 ??"),
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
        CastHook.Original(source, ptr);

        if (DalamudApi.ClientState.LocalPlayer == null) return;
        if (source != (Configuration.TargetMode ? DalamudApi.TargetManager.Target?.ObjectId : DalamudApi.ClientState.LocalPlayer?.ObjectId) || type != 1) return;
        PluginLog.Debug($"Casting:{action}:{type}");
        PluginUi.Cast((uint) action);
    }

    private void ReceiveActorControlSelf(uint entityId, uint type, uint buffID, uint direct, uint actionId,
        uint sourceId,
        uint arg4, uint arg5, ulong targetId, byte a10)
    {
        ActorControlSelfHook.Original(entityId, type, buffID, direct, actionId, sourceId, arg4, arg5, targetId, a10);
        if (type != 15) return;
        if (DalamudApi.ClientState.LocalPlayer == null) return;
        PluginLog.Debug($"Cancel:{entityId:X} {actionId}");
        if (entityId == (Configuration.TargetMode ? DalamudApi.TargetManager.Target?.ObjectId : DalamudApi.ClientState.LocalPlayer?.ObjectId)) PluginUi.Cancel(actionId);
    }

    private void ReceiveAbilityEffect(int sourceId, IntPtr sourceCharacter, IntPtr pos, IntPtr effectHeader,
        IntPtr effectArray, IntPtr effectTrail)
    {
        var action = Marshal.ReadInt32(effectHeader, 0x8);
        var type = Marshal.ReadByte(effectHeader, 31);
        ReceivAbilityHook.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTrail);

        if (DalamudApi.ClientState.LocalPlayer == null) return;
        if (sourceId != (Configuration.TargetMode ? DalamudApi.TargetManager.Target?.ObjectId : DalamudApi.ClientState.LocalPlayer?.ObjectId) || type != 1) return;
        PluginLog.Debug($"Do:{action}:{type}");
        PluginUi.DoAction((uint) action);
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