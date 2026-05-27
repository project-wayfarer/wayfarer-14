using Content.Client.Eui;
using Content.Shared._WF.Corporations;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._WF.Corporations.AdminEui;

[UsedImplicitly]
public sealed class CorpAdminEui : BaseEui
{
    private readonly CorpAdminWindow _window;

    public CorpAdminEui()
    {
        _window = new CorpAdminWindow();
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.OnRefresh += () => SendMessage(new CorpAdminEuiMsg.Refresh());
        _window.OnSetBalance += (corpId, balance) => SendMessage(new CorpAdminEuiMsg.SetBalance { CorporationId = corpId, NewBalance = balance });
        _window.OnSetDescription += (corpId, desc) => SendMessage(new CorpAdminEuiMsg.SetDescription { CorporationId = corpId, Description = desc });
        _window.OnSetPrivacy += (corpId, privacy) => SendMessage(new CorpAdminEuiMsg.SetPrivacy { CorporationId = corpId, Privacy = privacy });
        _window.OnKickMember += (corpId, userId) => SendMessage(new CorpAdminEuiMsg.KickMember { CorporationId = corpId, UserId = userId });
        _window.OnSetMemberRank += (corpId, userId, rank) => SendMessage(new CorpAdminEuiMsg.SetMemberRank { CorporationId = corpId, UserId = userId, Rank = rank });
        _window.OnDeleteCorporation += corpId => SendMessage(new CorpAdminEuiMsg.DeleteCorporation { CorporationId = corpId });
        _window.OnEvictStation += corpId => SendMessage(new CorpAdminEuiMsg.EvictStation { CorporationId = corpId });
        _window.OnSaveStation += corpId => SendMessage(new CorpAdminEuiMsg.SaveStation { CorporationId = corpId });
        _window.OnGrantStation += (corpId, name) => SendMessage(new CorpAdminEuiMsg.GrantStation { CorporationId = corpId, StationName = name });
        _window.OnCreateCorporation += (name, desc, privacy) => SendMessage(new CorpAdminEuiMsg.CreateCorporation { Name = name, Description = desc, Privacy = privacy });
        _window.OnAddMember += (corpId, userId) => SendMessage(new CorpAdminEuiMsg.AddMember { CorporationId = corpId, UserId = userId });
        _window.OnRecoverStation += (corpId, file, name) => SendMessage(new CorpAdminEuiMsg.RecoverStation { CorporationId = corpId, ArchiveFileName = file, StationName = name });
    }

    public override void Opened()
    {
        _window.OpenCentered();
    }

    public override void Closed()
    {
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not CorpAdminEuiState s)
            return;
        _window.UpdateState(s);
    }
}
