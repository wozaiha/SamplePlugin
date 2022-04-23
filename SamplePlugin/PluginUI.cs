using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Logging;
using ImGuiNET;
using ImGuiScene;
using Lumina.Excel;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace SkillDisplay;

internal class PluginUI : IDisposable
{
    public static Dictionary<uint, TextureWrap?> Icon = new();

    private readonly ExcelSheet<Action> Action = DalamudApi.DataManager.GetExcelSheet<Action>();
    private readonly Configuration config;

    public bool SettingsVisible = false;
    bool reset = false;

    private readonly List<Skill> Skilllist = new();
    private ImDrawListPtr window;
    private uint target = 0xE0000000;

    public PluginUI(Plugin p)
    {
        config = p.Configuration;
        DalamudApi.PluginInterface.UiBuilder.Draw += Draw;
        DalamudApi.PluginInterface.UiBuilder.Draw += DrawConfig;
        DalamudApi.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        if (!Icon.ContainsKey(0))
            Icon.TryAdd(0,
                DalamudApi.DataManager.GetImGuiTextureHqIcon(0));
        Icon.TryAdd(405,
            DalamudApi.DataManager.GetImGuiTextureHqIcon(101));
    }

    public void Dispose()
    {
        DalamudApi.PluginInterface.UiBuilder.Draw -= Draw;
        DalamudApi.PluginInterface.UiBuilder.Draw -= DrawConfig;
        DalamudApi.PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
        foreach (var (id, icon) in Icon) icon.Dispose();
    }

    public void DoAction(uint actionId)
    {
        try
        {
            var action = Action.GetRow(actionId)!;
            var iconId = action.Icon;
            if (!Icon.ContainsKey(iconId))
                Icon.TryAdd(iconId,
                    DalamudApi.DataManager.GetImGuiTextureHqIcon(iconId));
            Skilllist.Add(new Skill(action, DateTimeOffset.Now.ToUnixTimeMilliseconds(), Skill.ActionType.Do));
            PluginLog.Debug($"Adding:{action.RowId}:{action.ActionCategory.Row}");
        }
        catch (Exception e)
        {
            PluginLog.Error(e.ToString());
            throw;
        }
        
    }

    public void Cast(uint actionId)
    {
        try
        {
            var action = Action.GetRow(actionId)!;
            var iconId = action.Icon;
            if (!Icon.ContainsKey(iconId))
                Icon.TryAdd(iconId,
                    DalamudApi.DataManager.GetImGuiTextureHqIcon(iconId));
            Skilllist.Add(new Skill(action, DateTimeOffset.Now.ToUnixTimeMilliseconds(), Skill.ActionType.Cast));
        }
        catch (Exception e)
        {
            PluginLog.Error(e.ToString()); 
            throw;
        }
        
    }

    public void Cancel(uint actionId)
    {
        try
        {
            var action = Action.GetRow(actionId)!;
            var iconId = action.Icon;
            if (!Icon.ContainsKey(iconId))
                Icon.TryAdd(iconId,
                    DalamudApi.DataManager.GetImGuiTextureHqIcon(iconId));
            Skilllist.Add(new Skill(action, DateTimeOffset.Now.ToUnixTimeMilliseconds(), Skill.ActionType.Cancel));
            PluginLog.Debug($"Adding:{action.RowId}");
        }
        catch (Exception e)
        {
            PluginLog.Error(e.ToString());
            throw;
        }
    }


    public void DrawConfigUI()
    {
        SettingsVisible = true;
    }

    public void Draw()
    {
        ImGui.SetNextWindowBgAlpha(config.Alpha);
        if (config.TargetMode && DalamudApi.TargetManager.Target != null && target != DalamudApi.TargetManager.Target.ObjectId)
        {
            target = DalamudApi.TargetManager.Target.ObjectId;
            Skilllist.Clear();
        } 
        var flags = config.Lock
            ? ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMouseInputs | ImGuiWindowFlags.NoTitleBar
            : ImGuiWindowFlags.NoTitleBar;
        ImGui.Begin("main", flags);
        ImGui.SetWindowSize(new Vector2(config.IconSize * 10, config.IconSize * 1.5f),ImGuiCond.FirstUseEver);
        if (reset)
        {
            ImGui.SetWindowSize(new Vector2(config.IconSize * 10, config.IconSize * 1.5f));
            reset = false;
        }
        window = ImGui.GetWindowDrawList();
        var color = ImGui.ColorConvertFloat4ToU32(config.color);

        for (var i = Skilllist.Count - 1; i >= 0; i--)
        {
            var size = new Vector2(config.IconSize);
            var speed = size.X / 600;
            var skill = Skilllist[i];
            var pos = ImGui.GetWindowPos() + ImGui.GetWindowSize() - size -
                      new Vector2((DateTimeOffset.Now.ToUnixTimeMilliseconds() - skill.Time) * speed - size.X / 2f,
                          size.Y / 2);
            if (skill.Action.ActionCategory.Row is 4) // 能力
            {
                pos += new Vector2(0, size.Y / 2);
                size /= 1.5f;
            }

            if (skill.Action.ActionCategory.Row is 1) //自动攻击
            {
                pos += new Vector2(0, size.Y);
                size /= 2;
            }

            if (skill.Type == Skill.ActionType.Cast)
            {
                if (!config.ShowAuto && skill.Action.RowId is 7 or 8) continue;
                size *= 0.6f;
                var target = ImGui.GetWindowPos() + new Vector2(ImGui.GetWindowWidth(), pos.Y-ImGui.GetWindowPos().Y+size.Y  );
                for (var j = i + 1; j < Skilllist.Count; j++)
                {
                    if (Skilllist[j].Action.RowId != skill.Action.RowId) continue;
                    target = ImGui.GetWindowPos() + ImGui.GetWindowSize() - new Vector2(
                        (DateTimeOffset.Now.ToUnixTimeMilliseconds() - Skilllist[j].Time) * speed + config.IconSize / 2,
                        config.IconSize * 1.5f - size.Y);
                    break;
                }

                if (target.X - pos.X - size.X > 0) window.AddRectFilled(pos + new Vector2(size.X, 0), target, color);
            }

            //if ((i == 0) && (skill.Action.Cast100ms > 0) && (skill.Type == Skill.ActionType.Do))
            //{
            //    window.AddRectFilled(ImGui.GetWindowPos(),pos + size*0.6f, color);
            //}

            if (skill.Type != Skill.ActionType.Cancel)
            {
                if (!config.ShowAuto && skill.Action.RowId is 7 or 8) continue;
                window.AddImage(Icon[skill.Action.Icon]!.ImGuiHandle, pos, pos + size);
            }
            else window.AddImage(Icon[skill.Action.Icon]!.ImGuiHandle, pos, pos + Vector2.One);
            if ((DateTimeOffset.Now.ToUnixTimeMilliseconds() - skill.Time - 6000f) * speed > ImGui.GetWindowWidth())
                Skilllist.Remove(skill);
        }

        ImGui.End();
    }

    private void DrawConfig()
    {
        if (!SettingsVisible) return;
        ImGui.Begin("SkillDisplay Config", ref SettingsVisible, ImGuiWindowFlags.AlwaysAutoResize);
        var size = (int) config.IconSize;
        var changed = false;
        changed |= ImGui.Checkbox("Lock", ref config.Lock);
        ImGui.Text("Background Alpha:");
        ImGui.SameLine();
        changed |= ImGui.SliderFloat("###alpha",ref config.Alpha,0f,1f);
        ImGui.Text("Icon Size:");
        ImGui.SameLine();
        changed |= ImGui.InputInt("###Icon Size", ref size, 1);
        changed |= ImGui.ColorPicker4("Connection Color", ref config.color, ImGuiColorEditFlags.NoInputs);
        changed |= ImGui.Checkbox("Show Auto-attack.", ref config.ShowAuto);
        changed |= ImGui.Checkbox("Target Mode", ref config.TargetMode);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Will show nothing if no target is selected!");
        if (ImGui.Button("Reset Size")) reset = true;
        if (changed)
        {
            config.IconSize = size;
            config.Save();
        }

        ImGui.End();
    }

    public class Skill
    {
        public enum ActionType
        {
            Cast,
            Do,
            Cancel
        }

        public Action Action;
        public long Time;
        public ActionType Type;

        public Skill(Action action, long time, ActionType type)
        {
            Action = action;
            Time = time;
            Type = type;
        }
    }
}