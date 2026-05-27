using System.Threading.Tasks;
using Content.Server._NF.Bank;
using Content.Server.Database;
using Content.Server.Hands.Systems;
using Content.Server.Stack;
using Content.Shared._WF.Corporations;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Coordinates;
using Content.Shared.Stacks;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._WF.Corporations;

public sealed class CorporationAtmSystem : EntitySystem
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly StackSystem _stackSystem = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CorporationAtmComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<CorporationAtmComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<CorporationAtmComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<CorporationAtmComponent, CorporationAtmDepositMessage>(OnDeposit);
        SubscribeLocalEvent<CorporationAtmComponent, CorporationAtmWithdrawMessage>(OnWithdraw);
        SubscribeLocalEvent<CorporationAtmComponent, EntInsertedIntoContainerMessage>(OnCashSlotChanged);
        SubscribeLocalEvent<CorporationAtmComponent, EntRemovedFromContainerMessage>(OnCashSlotChanged);
    }

    private void OnComponentInit(EntityUid uid, CorporationAtmComponent comp, ComponentInit args)
    {
        _itemSlots.AddItemSlot(uid, CorporationAtmComponent.CashSlotId, comp.CashSlot);
    }

    private void OnComponentRemove(EntityUid uid, CorporationAtmComponent comp, ComponentRemove args)
    {
        _itemSlots.RemoveItemSlot(uid, comp.CashSlot);
    }

    private void OnUiOpened(EntityUid uid, CorporationAtmComponent comp, BoundUIOpenedEvent args)
    {
        _ = UpdateUiAsync(uid, comp, args.Actor, string.Empty);
    }

    private void OnDeposit(EntityUid uid, CorporationAtmComponent comp, CorporationAtmDepositMessage args)
    {
        _ = HandleDepositAsync(uid, comp, args);
    }

    private void OnWithdraw(EntityUid uid, CorporationAtmComponent comp, CorporationAtmWithdrawMessage args)
    {
        _ = HandleWithdrawAsync(uid, comp, args);
    }

    private void OnCashSlotChanged(EntityUid uid, CorporationAtmComponent comp, ContainerModifiedMessage args)
    {
        if (!TryComp<ActivatableUIComponent>(uid, out var uiComp) || uiComp.Key is null)
            return;

        var uiUsers = _uiSystem.GetActors(uid, uiComp.Key);
        foreach (var user in uiUsers)
        {
            _ = UpdateUiAsync(uid, comp, user, string.Empty);
        }
    }

    private async Task HandleDepositAsync(EntityUid uid, CorporationAtmComponent comp, CorporationAtmDepositMessage args)
    {
        var player = args.Actor;
        GetInsertedCashAmount(comp, out var deposit);

        if (!TryGetUserId(player, out var userId))
        {
            await UpdateUiAsync(uid, comp, player, "corp-atm-no-account");
            return;
        }

        if (deposit < 0)
        {
            _audio.PlayPvs(_audio.ResolveSound(comp.ErrorSound), uid);
            await UpdateUiAsync(uid, comp, player, "corp-atm-wrong-cash");
            return;
        }

        if (deposit == 0)
        {
            _audio.PlayPvs(_audio.ResolveSound(comp.ErrorSound), uid);
            await UpdateUiAsync(uid, comp, player, "corp-atm-no-cash");
            return;
        }

        if (comp.CashSlot.ContainerSlot is not BaseContainer cashSlot)
        {
            _audio.PlayPvs(_audio.ResolveSound(comp.ErrorSound), uid);
            await UpdateUiAsync(uid, comp, player, "corp-atm-no-cash");
            return;
        }

        var member = await _db.GetCorporationForPlayer(userId);
        if (member == null)
        {
            _audio.PlayPvs(_audio.ResolveSound(comp.ErrorSound), uid);
            await UpdateUiAsync(uid, comp, player, "corp-atm-not-member");
            return;
        }

        // Consume the cash stack and credit the corporation
        _containerSystem.CleanContainer(cashSlot);
        await _db.TryDepositToCorporation(member.Id, deposit);
        _audio.PlayPvs(_audio.ResolveSound(comp.ConfirmSound), uid);
        await UpdateUiAsync(uid, comp, player, string.Empty);
    }

    private async Task HandleWithdrawAsync(EntityUid uid, CorporationAtmComponent comp, CorporationAtmWithdrawMessage args)
    {
        var player = args.Actor;
        if (!TryGetUserId(player, out var userId))
        {
            await UpdateUiAsync(uid, comp, player, "corp-atm-no-account");
            return;
        }

        if (args.Amount <= 0)
        {
            await UpdateUiAsync(uid, comp, player, "corp-atm-invalid-amount");
            return;
        }

        var member = await _db.GetCorporationForPlayer(userId);
        if (member == null)
        {
            _audio.PlayPvs(_audio.ResolveSound(comp.ErrorSound), uid);
            await UpdateUiAsync(uid, comp, player, "corp-atm-not-member");
            return;
        }

        // Check rank — only Manager (2) or Leader (3) can withdraw
        var myMember = member.Members.Find(m => m.UserId == userId);
        if (myMember == null || myMember.Rank < 2)
        {
            _audio.PlayPvs(_audio.ResolveSound(comp.ErrorSound), uid);
            await UpdateUiAsync(uid, comp, player, "corp-atm-no-permission");
            return;
        }

        if (!await _db.TryWithdrawFromCorporation(member.Id, args.Amount))
        {
            _audio.PlayPvs(_audio.ResolveSound(comp.ErrorSound), uid);
            await UpdateUiAsync(uid, comp, player, "corp-atm-insufficient-corp-funds");
            return;
        }

        // Spawn physical spesos in the player's hands
        var stackPrototype = _prototypeManager.Index<StackPrototype>(comp.CashType);
        var cashStack = _stackSystem.Spawn(args.Amount, stackPrototype, player.ToCoordinates());
        if (!_hands.TryPickupAnyHand(player, cashStack))
            _transform.SetLocalRotation(cashStack, Angle.Zero);

        _audio.PlayPvs(_audio.ResolveSound(comp.ConfirmSound), uid);
        await UpdateUiAsync(uid, comp, player, string.Empty);
    }

    private async Task UpdateUiAsync(EntityUid uid, CorporationAtmComponent comp, EntityUid player, string statusKey)
    {
        GetInsertedCashAmount(comp, out var deposit);

        if (!TryGetUserId(player, out var userId))
        {
            _uiSystem.SetUiState(uid, CorporationAtmUiKey.Key,
                new CorporationAtmUiState(null, -1, 0, false, deposit, statusKey));
            return;
        }

        var corp = await _db.GetCorporationForPlayer(userId);

        if (corp == null)
        {
            _uiSystem.SetUiState(uid, CorporationAtmUiKey.Key,
                new CorporationAtmUiState(null, -1, 0, false, deposit, statusKey));
            return;
        }

        var myMember = corp.Members.Find(m => m.UserId == userId);
        var canWithdraw = myMember != null && myMember.Rank >= 2;

        _uiSystem.SetUiState(uid, CorporationAtmUiKey.Key,
            new CorporationAtmUiState(corp.Name, corp.Id, corp.Balance, canWithdraw, deposit, statusKey));
    }

    private void GetInsertedCashAmount(CorporationAtmComponent comp, out int amount)
    {
        amount = 0;
        var cashEntity = comp.CashSlot.ContainerSlot?.ContainedEntity;
        if (cashEntity is null)
            return;

        if (!TryComp<StackComponent>(cashEntity, out var stack) || stack.StackTypeId != comp.CashType)
        {
            amount = -1;
            return;
        }

        amount = stack.Count;
    }

    private bool TryGetUserId(EntityUid player, out Guid userId)
    {
        userId = Guid.Empty;
        if (!_playerManager.TryGetSessionByEntity(player, out var session))
            return false;
        userId = session.UserId.UserId;
        return true;
    }
}
