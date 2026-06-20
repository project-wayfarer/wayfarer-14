using Content.Client._WF.Administration.UI.PrayerLog;
using Content.Client.Eui;
using Content.Shared._WF.Administration.PrayerLog;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._WF.Administration.PrayerLog;

[UsedImplicitly]
public sealed class PrayerLogEui : BaseEui
{
    private readonly PrayerLogWindow _window;

    public PrayerLogEui()
    {
        _window = new PrayerLogWindow();
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.OnFollowRequested += uid => SendMessage(new PrayerLogEuiMsg.Follow(uid));
    }

    public override void Opened() => _window.OpenCentered();

    public override void Closed() => _window.Close();

    public override void HandleState(EuiStateBase state)
    {
        if (state is PrayerLogEuiState cast)
            _window.Populate(cast.Entries);
    }
}
