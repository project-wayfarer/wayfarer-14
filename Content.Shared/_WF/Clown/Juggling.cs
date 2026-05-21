using System.Collections.Generic;
using Content.Shared.Actions;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._WF.Clown;

public sealed partial class JuggleActionEvent : InstantActionEvent;

// Goes on the clown player. Grants the juggle action button.
[RegisterComponent]
public sealed partial class JugglingComponent : Component
{
    [DataField]
    public EntProtoId JuggleActionId = "ActionJuggle";

    [DataField]
    public EntityUid? JuggleAction;
}

// Added to the clown only while juggling. Replicated so clients can draw the items in motion.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class JugglingActiveComponent : Component
{
    [AutoNetworkedField]
    public TimeSpan StartTime;

    [AutoNetworkedField]
    public List<NetEntity> JuggledItems = new();
}

// Consumes the Walk key while the session's player is juggling, so pressing
// it does nothing. Otherwise the key is handled normally. Used by both the
// server and client juggling systems.
public sealed class JuggleWalkBlocker : InputCmdHandler
{
    public override bool HandleCmdMessage(IEntityManager entManager, ICommonSession? session, IFullInputCmdMessage message)
    {
        if (session?.AttachedEntity is not { } player)
            return false;

        return entManager.HasComponent<JugglingActiveComponent>(player);
    }
}
