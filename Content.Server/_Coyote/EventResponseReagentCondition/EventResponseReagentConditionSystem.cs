using System.Linq;

namespace Content.Server._Coyote.EventResponseReagentCondition;

/// <summary>
/// This handles...
/// </summary>
public sealed class EventResponseReagentConditionSystem : EntitySystem
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<EventResponseConditionComponent, EntityEffectConditionMessageEvent>(RespondToEvent);
        SubscribeLocalEvent<TheobromineIntoleranceComponent, EntityEffectConditionMessageEvent>(RespondToEvent);
        SubscribeLocalEvent<AllicinIntoleranceComponent, EntityEffectConditionMessageEvent>(RespondToEvent);
    }

    /// <summary>
    /// Since all the components do the same damn thing with the same damn data,
    /// but technically are different components, we have to convert the
    /// incoming component data into something common.
    /// </summary>
   public void RespondToEvent(
        EntityUid uid,
        EventResponseConditionComponent component,
        ref EntityEffectConditionMessageEvent args)
    {
        HandleEventResponse(component.Responses, component.MessageTriggers, args);
    }

    /// <inheritdoc/>
    public void RespondToEvent(
        EntityUid uid,
        TheobromineIntoleranceComponent component,
        ref EntityEffectConditionMessageEvent args)
    {
        HandleEventResponse(component.Responses, component.MessageTriggers, args);
    }

    /// <inheritdoc/>
    public void RespondToEvent(
        EntityUid uid,
        AllicinIntoleranceComponent component,
        ref EntityEffectConditionMessageEvent args)
    {
        HandleEventResponse(component.Responses, component.MessageTriggers, args);
    }

    /// This is the actual response handler, which checks if the message
    /// matches any of the triggers, and adds the responses if it does.
    private void HandleEventResponse(
        List<string> responses,
        List<string> messageTriggers,
        EntityEffectConditionMessageEvent args)
    {
        if (!messageTriggers.Any(trigger => args.Message.Contains(trigger)))
            return;
        foreach (var response in responses)
        {
            args.AddResponse(response);
        }
    }
}

// the event!
public sealed class EntityEffectConditionMessageEvent(
    EntityUid targetEntity,
    string message) : EntityEventArgs
{
    public EntityUid TargetEntity { get; } = targetEntity;
    public string Message { get; } = message;
    public List<string> Responses { get; } = new();

    public void AddResponse(string response)
    {
        if (HasResponse(response))
            return;
        Responses.Add(response);
    }

    public bool HasResponse(string response)
    {
        return Responses.Contains(response);
    }
}
