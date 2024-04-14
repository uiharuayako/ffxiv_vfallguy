using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using ImGuiNET;
using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Colors;

namespace vfallguy;

public class MainWindow : Window, IDisposable
{
    private GameEvents _gameEvents = new();
    private DebugDrawer _drawer = new();
    private AutoJoinLeave _automation = new();
    private Map? _map;
    private DateTime _now;
    private Vector3 _prevPos;
    private Vector3 _movementDirection;
    private float _movementSpeed;
    private bool _autoJoin;
    private bool _autoLeaveIfNotSolo;
    private bool _showAOEs;
    private bool _showAOEText;
    private bool _showPathfind;
    private DateTime _autoJoinAt = DateTime.MaxValue;
    private DateTime _autoLeaveAt = DateTime.MaxValue;
    private int _numPlayersInDuty;
    private float _autoJoinDelay = 0.5f;
    private float _autoLeaveDelay = 3;
    private float _hackMoveSpeed = 1;
    private int _autoLeaveLimit = 1;
    private bool _enableHacks = false;

    public MainWindow() : base("vfailguy with MiniHacks")
    {
        ShowCloseButton = false;
        RespectCloseHotkey = false;
    }

    public void Dispose()
    {
        _map?.Dispose();
        _gameEvents.Dispose();
        _automation.Dispose();
    }

    public unsafe override void PreOpenCheck()
    {
        _automation.Update();
        _drawer.Update();

        _now = DateTime.Now;
        var playerPos = Service.ClientState.LocalPlayer?.Position ?? new();
        _movementDirection = playerPos - _prevPos;
        _prevPos = playerPos;
        _movementSpeed = _movementDirection.Length() / Framework.Instance()->FrameDeltaTime;
        _movementDirection = _movementDirection.NormalizedXZ();

        IsOpen = Service.ClientState.TerritoryType is 1165 or 1197;
        if (!IsOpen && _enableHacks)
        {
            MiniHacks.Disable();
            _enableHacks = false;
        }

        if (IsOpen && !_enableHacks)
        {
            _enableHacks = true;
        }

        UpdateMap();
        UpdateAutoJoin();
        UpdateAutoLeave();
        DrawOverlays();

        _drawer.DrawWorldPrimitives();
    }

    public unsafe override void Draw()
    {
        ImGui.TextColored(ImGuiColors.HealerGreen, "vfallguy插件开源免费，请不要以任何方式付费购买，如果你身边有人买了，请嘲笑他");
        ImGui.TextColored(ImGuiColors.DPSRed, "闲鱼小店：文-两仪式，倒卖vfailguy原版插件，看到记得举报");
        ImGui.TextColored(ImGuiColors.HealerGreen, "如果进本功能没反应，请手动进一次本再退");

        if (ImGui.Button("立即排糖豆人本"))
            _automation.RegisterForDuty();
        ImGui.SameLine();
        if (ImGui.Button("退本"))
            _automation.LeaveDuty();
        ImGui.SameLine();
        ImGui.TextUnformatted(
            $"本里人数: {_numPlayersInDuty} ({(_autoLeaveAt == DateTime.MaxValue ? "无退本计划" : $"在 {(_autoLeaveAt - _now).TotalSeconds:f1}s后自动退本")})");

        ImGui.Checkbox("自动排糖豆人本", ref _autoJoin);
        if (_autoJoin)
        {
            using (ImRaii.PushIndent())
            {
                ImGui.SliderFloat("延迟（秒）###j", ref _autoJoinDelay, 0, 10);
            }
        }

        ImGui.Checkbox("不是单人就退出", ref _autoLeaveIfNotSolo);
        if (_autoLeaveIfNotSolo)
        {
            using (ImRaii.PushIndent())
            {
                ImGui.SliderFloat("延迟（秒）###l", ref _autoLeaveDelay, 0, 10);
                ImGui.SliderInt("当多于X人时退出", ref _autoLeaveLimit, 1, 23);
            }
        }

        ImGui.Checkbox("显示AOE范围", ref _showAOEs);
        ImGui.Checkbox("显示AOE Debug消息", ref _showAOEText);
        ImGui.Checkbox("显示推荐路线", ref _showPathfind);

        if (_map != null)
        {
            var strats = _map.Strats();
            if (strats.Length > 0)
                ImGui.TextUnformatted(strats);
            ImGui.TextUnformatted($"位置: {_map.PlayerPos}");
            ImGui.TextUnformatted($"路径: {_map.PathSkip}-{_map.Path.Count}");
            ImGui.TextUnformatted($"速度: {_movementSpeed}");

            //foreach (var aoe in _map.AOEs.Where(aoe => aoe.NextActivation != default))
            //{
            //    var nextActivation = (aoe.NextActivation - _now).TotalSeconds;
            //    using (ImRaii.PushColor(ImGuiCol.Text, nextActivation < 0 ? 0xff0000ff : 0xffffffff))
            //        ImGui.TextUnformatted($"{aoe.Type} R{aoe.R1} @ {aoe.Origin}: activate in {nextActivation:f3}, repeat={aoe.Repeat}, seqd={aoe.SeqDelay}");
            //}
        }

        if (ImGui.CollapsingHeader("底裤功能（不是单人时开，后果自负！）"))
        {
            if (ImGui.InputFloat("移速设置", ref _hackMoveSpeed))
            {
                if (_hackMoveSpeed > 5) _hackMoveSpeed = 5;
            }

            if (ImGui.Button("应用移速设置"))
            {
                MiniHacks.MoveSpeed(_hackMoveSpeed);
            }

            ImGui.SameLine();
            if (ImGui.Button("恢复移速"))
            {
                _hackMoveSpeed = 1;
                MiniHacks.MoveSpeed(_hackMoveSpeed);
            }

            if (ImGui.Checkbox("防击退", ref MiniHacks.EnableAntiKick))
            {
                MiniHacks.SetAntiKick(MiniHacks.EnableAntiKick);
            }
        }
    }

    private void UpdateMap()
    {
        if (Service.Condition[ConditionFlag.BetweenAreas])
            return;

        Type? mapType = null;
        if (IsOpen)
        {
            if (Service.ClientState.TerritoryType == 1197)
            {
                //mapType = typeof(MapTest);
            }
            else
            {
                var pos = Service.ClientState.LocalPlayer!.Position;
                mapType = pos switch
                {
                    //{ X: >= -20 and <= 20, Z: >= -400 and <= -100 } => typeof(Map1A),
                    { X: >= -40 and <= 40, Z: >= 100 and <= 350 } => typeof(Map3),
                    _ => null
                };
            }
        }

        if (_map?.GetType() != mapType)
        {
            _map?.Dispose();
            _map = null;
            if (mapType != null)
                _map = (Map?)Activator.CreateInstance(mapType, _gameEvents);
        }

        _map?.Update();
    }

    private void UpdateAutoJoin()
    {
        bool wantAutoJoin = _autoJoin && _automation.Idle && IsOpen && Service.ClientState.TerritoryType == 1197 &&
                            !Service.Condition[ConditionFlag.WaitingForDutyFinder] &&
                            !Service.Condition[ConditionFlag.BetweenAreas];
        if (!wantAutoJoin)
        {
            _autoJoinAt = DateTime.MaxValue;
        }
        else if (_autoJoinAt == DateTime.MaxValue)
        {
            Service.Log.Debug($"Auto-joining in {_autoJoinDelay:f2}s...");
            _autoJoinAt = _now.AddSeconds(_autoJoinDelay);
        }
        else if (_now >= _autoJoinAt)
        {
            Service.Log.Debug($"Auto-joining");
            _automation.RegisterForDuty();
            _autoJoinAt = DateTime.MaxValue;
        }
    }

    private void UpdateAutoLeave()
    {
        _numPlayersInDuty = Service.ClientState.TerritoryType == 1165 && Service.Condition[ConditionFlag.BoundByDuty] &&
                            !Service.Condition[ConditionFlag.BetweenAreas]
            ? Service.ObjectTable.Count(o => o.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player)
            : 0;
        bool wantAutoLeave = _autoLeaveIfNotSolo && _numPlayersInDuty > _autoLeaveLimit && _automation.Idle;
        if (!wantAutoLeave)
        {
            _autoLeaveAt = DateTime.MaxValue;
        }
        else if (_autoLeaveAt == DateTime.MaxValue)
        {
            Service.Log.Debug($"在 {_autoLeaveDelay:f2}s 后自动退本");
            _autoLeaveAt = _now.AddSeconds(_autoLeaveDelay);
        }
        else if (_now >= _autoLeaveAt)
        {
            Service.Log.Debug($"Auto-leaving: {_numPlayersInDuty} players");
            _automation.LeaveDuty();
            _autoLeaveAt = DateTime.MaxValue;
        }
    }

    private void DrawOverlays()
    {
        if (_map == null || Service.Condition[ConditionFlag.BetweenAreas])
            return;

        if (_showPathfind)
        {
            var from = _map.PlayerPos;
            for (int i = _map.PathSkip; i < _map.Path.Count; ++i)
            {
                var wp = _map.Path[i];
                var delay = (wp.StartMoveAt - _now).TotalSeconds;
                _drawer.DrawWorldLine(from, wp.Dest, i > 0 ? 0xff00ffff : delay <= 0 ? 0xff00ff00 : 0xff0000ff);
                if (delay > 0)
                    _drawer.DrawWorldText(from, 0xff0000ff, $"{delay:f3}");
                from = wp.Dest;
            }
        }

        foreach (var aoe in _map.AOEs.Where(aoe => aoe.NextActivation != default))
        {
            var nextActivation = (aoe.NextActivation - _now).TotalSeconds;
            if (nextActivation < 2.5f)
            {
                var (aoeEnter, aoeExit) = _movementSpeed > 0 ? aoe.Intersect(_map.PlayerPos, _movementDirection) :
                    aoe.Contains(_map.PlayerPos) ? (0, float.PositiveInfinity) : (float.NaN, float.NaN);
                var delay = !float.IsNaN(aoeEnter)
                    ? aoe.ActivatesBetween(_now, aoeEnter * Map.InvSpeed - 0.1f, aoeExit * Map.InvSpeed + 0.1f)
                    : 0;
                var color = delay > 0 ? 0xff0000ff : 0xff00ffff;
                if (_showAOEs)
                {
                    aoe.Draw(_drawer, color);
                }

                if (_showAOEText)
                {
                    var text =
                        $"{nextActivation:f3} [{aoeEnter * Map.InvSpeed:f2}-{aoeExit * Map.InvSpeed:f2}, {delay:f2}]";
                    var dir = (aoe.Origin - _map.PlayerPos).NormalizedXZ();
                    var (enter, exit) = aoe.Intersect(_map.PlayerPos, dir);
                    var textPos = _map.PlayerPos + dir * MathF.Max(enter, 0);
                    _drawer.DrawWorldText(textPos, color, text);
                }
            }
        }
    }
}