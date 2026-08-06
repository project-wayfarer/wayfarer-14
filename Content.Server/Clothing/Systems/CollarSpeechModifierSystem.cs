using Content.Server.Chat.Systems;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;
using System.Text.RegularExpressions;
using static Content.Shared.Inventory.InventorySystem;

namespace Content.Server.Clothing.Systems;

/// <summary>
/// System that overrides the wearer's speech with other text
/// </summary>
public sealed class CollarSpeechModifierSystem : EntitySystem
{
    private Regex _endingPunctRegex = new Regex("[.!?~]+\\Z");
    private Regex _endingSpecialPunctRegex = new Regex("[!?~]+\\Z");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TransformSpeechEvent>(OnSpeakAttempt);
    }

    private void OnSpeakAttempt(TransformSpeechEvent args)
    {
        if (TryComp<InventoryComponent>(args.Sender, out var comp))
        {
            // Ensure user is wearing a collar with the correct component
            var enumerator = new InventorySlotEnumerator(comp, SlotFlags.NECK);
            CollarSpeechModifierComponent? collar = null;
            while (enumerator.NextItem(out var item))
            {
                TryComp<CollarSpeechModifierComponent>(item, out collar);
            }

            // Ensure collar is equipped, active and valid
            if (collar == null || collar.SpeechTypes == null || collar.SelectedType == null || collar.SelectedType == "Off" || !collar.SpeechTypes.ContainsKey(collar.SelectedType))
            {
                return;
            }

            if (collar.SelectedType == "Mute")
            {
                var match = _endingPunctRegex.Match(args.Message);
                // Mute should just display ellipsis. This could cancel the speech event altogether, but this way allows some limited control over punctuation.
                args.Message = "...";
                var punct = _endingPunctRegex.Match(args.Message);

                if (match.Success)
                {
                    args.Message += match.Value;
                }
            }
            else
            {
                var messageLength = args.Message.Split(' ').Length / 2;
                var message = PickRandom(collar.SpeechTypes[collar.SelectedType]);

                while (messageLength > 1)
                {
                    message += $" {PickRandom(collar.SpeechTypes[collar.SelectedType]).ToLower()}";
                    messageLength--;
                }

                // Preserve ending punctionation
                var match = _endingPunctRegex.Match(args.Message);
                if (string.IsNullOrEmpty(match.Value))
                {
                    message += ".";
                }

                if (match.Success)
                {
                    message += match.Value;
                }
                args.Message = message;
            }
        }
    }

    private string PickRandom(List<string>? list) {
        if (list == null)
        {
            return "";
        }
        return list[new Random().Next(list.Count)];
    }
}
