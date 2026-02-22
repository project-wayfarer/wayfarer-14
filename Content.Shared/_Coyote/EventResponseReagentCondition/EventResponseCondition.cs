using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Coyote.EventResponseReagentCondition;

public sealed partial class EventResponse : EntityEffectCondition
{
    [DataField(required: true)]
    public string Message;

    [DataField(required: true)]
    public string Response;

    [DataField]
    public string GuidebookHelpthing = "NULL!!!";

    public override bool Condition(EntityEffectBaseArgs args)
    {
        // send an event to the target entity, to read back the response
        var ev = new EntityEffectConditionMessageEvent(args.TargetEntity, Message);
        args.EntityManager.EventBus.RaiseLocalEvent(
            args.TargetEntity,
            ev,
            true);
        return ev.HasResponse(Response);
    }

    public override string GuidebookExplanation(IPrototypeManager prototype)
    {
        return GuidebookHelpthing; // localization is for losers
    }
}
