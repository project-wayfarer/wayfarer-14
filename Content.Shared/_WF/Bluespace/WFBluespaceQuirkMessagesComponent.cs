using Robust.Shared.GameStates;

namespace Content.Shared._WF.Bluespace;

/// <summary>
/// Periodically shows a random subtle popup message to whoever is currently
/// holding/wearing/using a bluespace container. Story flavor.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WFBluespaceQuirkMessagesComponent : Component
{
    /// <summary>
    /// Minimum delay between messages.
    /// </summary>
    [DataField]
    public TimeSpan MinInterval = TimeSpan.FromMinutes(7);

    /// <summary>
    /// Maximum delay between messages.
    /// </summary>
    [DataField]
    public TimeSpan MaxInterval = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Localization IDs of possible messages, picked at random per fire.
    /// </summary>
    [DataField]
    public List<string> Messages = new()
    {
        "wf-bluespace-quirk-message-1",
        "wf-bluespace-quirk-message-2",
        "wf-bluespace-quirk-message-3",
        "wf-bluespace-quirk-message-4",
        "wf-bluespace-quirk-message-5",
        "wf-bluespace-quirk-message-6",
        "wf-bluespace-quirk-message-7",
        "wf-bluespace-quirk-message-8",
    };

    /// <summary>
    /// The next time a message should be shown. Set on first update.
    /// </summary>
    [DataField]
    public TimeSpan? NextMessageTime;
}
