using System.Linq;
using System.Text.RegularExpressions;
using Content.Client.CharacterInfo;
using Content.Shared.CCVar;
using Content.Shared._DV.CCVars;
using Content.Shared.Dataset;
using Content.Shared.Chat;
using Content.Shared.Chat.TypingIndicator;
using Robust.Shared.Prototypes;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using static Content.Client.CharacterInfo.CharacterInfoSystem;

namespace Content.Client.UserInterface.Systems.Chat;

public sealed partial class ChatUIController : IOnSystemChanged<CharacterInfoSystem>
{
    public ChatSelectChannel CurrentChannel = ChatSelectChannel.None;
    private static readonly ProtoId<TypingIndicatorPrototype> WhisperID = "whisper";
    private static readonly ProtoId<TypingIndicatorPrototype> EmoteID = "emote";
    private static readonly ProtoId<TypingIndicatorPrototype> OocID = "ooc";
    private static readonly ProtoId<TypingIndicatorPrototype> RadioID = "radio";

    /// <summary>
    ///     Gets Invoked whenever the autofilled highlights have changed.
    ///     Used to populate the preview in the channel selector window.
    /// </summary>
    public event Action<string>? OnAutoHighlightsUpdated;

    /// <summary>
    ///     A list of words to be highlighted in the chatbox.
    ///     Auto-generated from users's character information.
    /// </summary>
    private string _autoHighlights = String.Empty;

    /// <summary>
    /// Returns the list of auto-generated highlights based on the character's info (job, name, etc). Returns an empty list if the option is disabled.
    /// </summary>
    internal string AutoHighlights => _autoFillHighlightsEnabled ? _autoHighlights : String.Empty;

    internal string CurrentUserHighlights => _config.GetCVar(CCVars.ChatHighlights);

    /// <summary>
    /// Gets whether the player has auto-generated highlights enabled or not.
    /// </summary>
    internal bool AutoHighlightsEnabled => _autoFillHighlightsEnabled;

    /// <summary>
    /// This is ugly but it's only going to be around until the next upstream merge.
    /// We'll still use CCVars.ChatHighlights.
    /// </summary>
    private void MigrateDVHighlightSettings()
    {
        // AutoHightligt Checkbox
        bool shouldSave = false;
        var oldDCCAutofillFlag = _config.GetCVar(CCVars.ChatAutoFillHighlights);
        if (oldDCCAutofillFlag)
        {
            _config.SetCVar(CCVars.ChatAutoFillHighlights, oldDCCAutofillFlag);
            _config.SetCVar(CCVars.ChatAutoFillHighlights, false); // Next time this runs, it won't migrate this value again since CCVars.ChatAutoFillHighlights defaults to false.
            shouldSave = true;
        }

        // Chat Color
        var oldDCCVarColor = _config.GetCVar(CCVars.ChatHighlightsColor);
        var defaultColor = "#17FFC1FF";
        if (!oldDCCVarColor.Equals(defaultColor)) // Default value
        {
            _config.SetCVar(CCVars.ChatHighlightsColor, oldDCCVarColor); // Next time, it should equal those words
            _config.SetCVar(CCVars.ChatHighlightsColor, defaultColor); // Prevents it from running again
            shouldSave = true;
        }

        if (shouldSave)
            _config.SaveToFile();
    }

    /// <summary>
    ///     Notifies and sets what type of typing indicator should be put.
    /// </summary>
    public void NotifySpecificChatTextChange(ChatSelectChannel selectedChannel)
    {
        var channel = CurrentChannel;
        if (CurrentChannel == ChatSelectChannel.None)
            channel = selectedChannel;

        switch (channel)
        {
            case ChatSelectChannel.Whisper:
                _typingIndicator?.ClientAlternateTyping(WhisperID);
                break;

            case ChatSelectChannel.Radio:
                _typingIndicator?.ClientAlternateTyping(RadioID);
                break;

            case ChatSelectChannel.Emotes:
                _typingIndicator?.ClientAlternateTyping(EmoteID);
                break;

            case ChatSelectChannel.LOOC:
            case ChatSelectChannel.OOC:
            case ChatSelectChannel.SubtleLOOC: // Wayfarer
            case ChatSelectChannel.ShipOOC: // Wayfarer
                _typingIndicator?.ClientAlternateTyping(OocID);
                break;

            default:
                _typingIndicator?.ClientChangedChatText();
                break;
        }
    }
}
