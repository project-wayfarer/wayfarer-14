using Content.Shared._WF.Corporations;
using Robust.Client.UserInterface;

namespace Content.Client._WF.Corporations;

public sealed class CorporationAtmBoundUserInterface : BoundUserInterface
{
    private CorporationAtmMenu? _menu;

    public CorporationAtmBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) {}

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<CorporationAtmMenu>();
        _menu.DepositRequest += () => SendMessage(new CorporationAtmDepositMessage());
        _menu.WithdrawRequest += amount => SendMessage(new CorporationAtmWithdrawMessage(amount));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is CorporationAtmUiState atmState)
            _menu?.UpdateState(atmState);
    }
}
