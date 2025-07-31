namespace Content.Shared._Coyote.EventResponseReagentCondition;

/// <summary>
/// HEY YOU WANT TO ADD A NEW EVENT RESPONSE CONDITION?
/// JUST MAKE A NEW COMPONENT THAT INHERITS FROM THIS ONE
///
/// THEN IN THE SYSTEM, ADD A SUBSCRIBELOCALEVENT FOR YOUR COMPONENT
/// AND CALL THE RESPONDTOEVENT METHOD WITH YOUR COMPONENT TYPE
/// AND IT WILL AUTOMATICALLY HANDLE THE RESPONSES FOR YOU
///
/// JUST COPYPASTE THE RESPONDTOEVENT METHOD
/// AND CHANGE THE COMPONENT TYPE TO YOURS
/// AND IT WILL WORK
///
/// I SWEAR THIS IS A GOOD IDEA
/// EAT MY ASS
/// </summary>
[RegisterComponent, Virtual]
public partial class EventResponseConditionComponent : Component
{
    public readonly List<string> MessageTriggers = [];
    public readonly List<string> Responses = [];
}

[RegisterComponent]
public sealed partial class TheobromineIntoleranceComponent : EventResponseConditionComponent
{
    public new readonly List<string> MessageTriggers =
    [
        "TheobromineIntolerance",
    ];
    public new readonly List<string> Responses =
    [
        "Vomit",
        "Damage",
    ];
}

[RegisterComponent]
public sealed partial class AllicinIntoleranceComponent : EventResponseConditionComponent
{
    public new readonly List<string> MessageTriggers =
    [
        "AllicinIntolerance",
    ];
    public new readonly List<string> Responses =
    [
        "Vomit",
        "Damage",
    ];
}
