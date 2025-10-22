using Content.Shared.Mobs;

namespace Content.Server.Explosion.Components;

/// <summary>
/// Use where you want something to trigger on mobstate change
/// </summary>
[RegisterComponent]
public sealed partial class TriggerOnMobstateChangeComponent : Component
{
    /// <summary>
    /// What state should trigger this?
    /// </summary>
    [ViewVariables]
    [DataField("mobState", required: true)]
    public List<MobState> MobState = new();

    /// <summary>
    /// If true, prevents suicide attempts for the trigger to prevent cheese.
    /// </summary>
    [ViewVariables]
    [DataField("preventSuicide")]
    public bool PreventSuicide = false;

    /// <summary>
    /// If true, it will not do its thing if the owner has been vored, owo.
    /// </summary>
    [ViewVariables]
    [DataField("preventVore")]
    public bool PreventVore = false;

    /// <summary>
    /// If false, this component will not trigger / is not allowed to work.
    /// </summary>
    [ViewVariables]
    [DataField("enabled")]
    public bool Enabled = true;
}
