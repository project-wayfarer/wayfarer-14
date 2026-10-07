using Content.Client.Eui;
using Content.Client.Lobby.UI.Loadouts;
using Content.Shared._WF.Administration.CentComSpawn;
using Content.Shared.Clothing;
using Content.Shared.Eui;
using Content.Shared.Preferences.Loadouts;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._WF.Administration.CentComSpawn;

[UsedImplicitly]
public sealed class CentComSpawnEui : BaseEui
{
    private static readonly ProtoId<RoleLoadoutPrototype> Role = LoadoutSystem.GetJobPrototype(CentComSpawnJob.Id);

    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private LoadoutWindow? _window;

    public CentComSpawnEui()
    {
        IoCManager.InjectDependencies(this);
    }

    public override void Closed()
    {
        _window?.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not CentComSpawnEuiState cast
            || _window != null
            || _playerManager.LocalSession is not { } session
            || !_prototypeManager.TryIndex(Role, out var roleProto))
            return;

        var collection = IoCManager.Instance!;
        var profile = cast.Profile;

        var loadout = profile.Loadouts.TryGetValue(Role, out var saved) ? saved.Clone() : new RoleLoadout(Role);
        if (saved == null)
            loadout.SetDefault(profile, session, _prototypeManager);

        _window = new LoadoutWindow(profile, loadout, roleProto, session, collection)
        {
            Title = Loc.GetString("wf-centcom-spawn-window-title"),
        };

        _window.OnLoadoutPressed += (group, item) =>
        {
            loadout.AddLoadout(group, item, _prototypeManager);
            _window.RefreshLoadouts(loadout, session, collection);
        };

        _window.OnLoadoutUnpressed += (group, item) =>
        {
            loadout.RemoveLoadout(group, item, _prototypeManager);
            _window.RefreshLoadouts(loadout, session, collection);
        };

        var spawnButton = new Button
        {
            Text = Loc.GetString("wf-centcom-spawn-button"),
            Margin = new Thickness(5),
        };
        spawnButton.OnPressed += _ => SendMessage(new CentComSpawnEuiMsg(loadout));

        _window.FindControl<BoxContainer>("RoleNameBox").Parent!.AddChild(spawnButton);
        spawnButton.SetPositionInParent(0);

        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.RefreshLoadouts(loadout, session, collection);
        _window.OpenCentered();
    }
}
