using Content.Server.Administration;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared._WF.Administration.PrayerLog;
using Content.Shared.Eui;
using Content.Shared.Follower;
using Content.Shared.Ghost;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server._WF.Administration.PrayerLog;

public sealed class PrayerLogEui : BaseEui
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystems = default!;

    private readonly PrayerLogSystem _prayerLog;
    private readonly FollowerSystem _followerSystem;

    public PrayerLogEui()
    {
        IoCManager.InjectDependencies(this);
        _prayerLog = _entitySystems.GetEntitySystem<PrayerLogSystem>();
        _followerSystem = _entitySystems.GetEntitySystem<FollowerSystem>();
    }

    public override void Opened()
    {
        _prayerLog.EntryRecorded += StateDirty;
        StateDirty();
    }

    public override void Closed()
    {
        _prayerLog.EntryRecorded -= StateDirty;
        base.Closed();
    }

    public override EuiStateBase GetNewState() => new PrayerLogEuiState(_prayerLog.GetEntries());

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not PrayerLogEuiMsg.Follow follow)
            return;

        if (Player.AttachedEntity is not { } adminEntity
            || !_entityManager.HasComponent<GhostComponent>(adminEntity))
            return;

        if (!_playerManager.TryGetSessionById(follow.UserId, out var target)
            || target.AttachedEntity is not { } targetEntity)
            return;

        _followerSystem.StartFollowingEntity(adminEntity, targetEntity);
    }
}

[AdminCommand(AdminFlags.Adminhelp)]
public sealed class PrayerLogCommand : LocalizedEntityCommands
{
    [Dependency] private readonly EuiManager _euiManager = default!;

    public override string Command => "prayerlog";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        _euiManager.OpenEui(new PrayerLogEui(), player);
    }
}
