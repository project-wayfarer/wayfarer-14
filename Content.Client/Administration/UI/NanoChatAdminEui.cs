using Content.Client.Eui;
using Content.Shared.Administration;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client.Administration.UI;

[UsedImplicitly]
public sealed class NanoChatAdminEui : BaseEui
{
    private NanoChatAdminWindow? _window;

    public override void Opened()
    {
        base.Opened();
        
        _window = new NanoChatAdminWindow();
        _window.OnRefreshPressed += OnRefreshPressed;
        _window.OnCardSelected += OnCardSelected;
        _window.OnClose += OnWindowClosed;
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        _window?.Close();
        _window = null;
    }

    public override void HandleState(EuiStateBase state)
    {
        base.HandleState(state);

        if (state is not NanoChatAdminEuiState nanoChatState)
            return;

        _window?.UpdateState(nanoChatState);
    }

    private void OnRefreshPressed()
    {
        SendMessage(new NanoChatAdminEuiMsg.Refresh());
    }

    private void OnCardSelected(NetEntity cardEntity)
    {
        SendMessage(new NanoChatAdminEuiMsg.SelectCard { CardEntity = cardEntity });
    }

    private void OnWindowClosed()
    {
        SendMessage(new CloseEuiMessage());
    }
}
