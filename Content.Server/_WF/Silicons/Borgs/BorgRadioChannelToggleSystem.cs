using Content.Server.Radio;
using Content.Shared.Radio.EntitySystems;
using Content.Shared.Silicons.Borgs.Components;

namespace Content.Server._WF.Silicons.Borgs;

/// <summary>
/// Enforces a borg's muted radio channels, since the stock channel-disable is only checked for worn headsets, not a borg's intrinsic radio.
/// </summary>
public sealed class BorgRadioChannelToggleSystem : EntitySystem
{
    [Dependency] private readonly DisabledRadioChannelsSystem _disabledChannels = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BorgChassisComponent, RadioReceiveAttemptEvent>(OnReceiveAttempt);
        SubscribeLocalEvent<BorgChassisComponent, RadioSendAttemptEvent>(OnSendAttempt);
    }

    private void OnReceiveAttempt(EntityUid uid, BorgChassisComponent component, ref RadioReceiveAttemptEvent args)
    {
        if (_disabledChannels.IsChannelDisabled(uid, args.Channel.ID))
            args.Cancelled = true;
    }

    private void OnSendAttempt(EntityUid uid, BorgChassisComponent component, ref RadioSendAttemptEvent args)
    {
        if (_disabledChannels.IsChannelDisabled(uid, args.Channel.ID))
            args.Cancelled = true;
    }
}
