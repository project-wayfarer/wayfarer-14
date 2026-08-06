using Content.Shared.Clothing.EntitySystems;
using Robust.Shared.GameStates;

namespace Content.Shared.Clothing.Components;

/// <summary>
/// When applied to a collar (or other neck slot item) transforms the wearer's speech. 
/// </summary>
[RegisterComponent]
[NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class CollarSpeechModifierComponent : Component
{
    /// <summary>
    /// This data structure determines what replacements should be used
    /// </summary>
    public Dictionary<string, List<string>> SpeechTypes => new Dictionary<string, List<string>>()
    {
        {"Off", new List<string>() { "..." }  },
        {"Mute", new List<string>() { "..." }  },
        {"Muffled", new List<string>() { "Mmf", "Mmphf", "Mmh", "Mf", "Nmf", "Mphf" }  },
        {"Dog", new List<string>() { "Woof", "Aruff", "Ruff", "Arf", "Wruff"  }  },
        {"Cat", new List<string>() { "Meow", "Nya", "Mrow", "Miaow", "Mrrp", "Mrr", "Mya" }  },
        {"Bird", new List<string>() { "Squawk", "Cheep", "Caw", "Tweet", "Peep" }  },
        {"Chicken", new List<string>() { "Squawk", "Bawk", "Cluck", "Buk", "Bukawk" }  },
        {"Rat", new List<string>() { "Squeak", "Piep", "Squee", "Squeek", "Pip" }  },
        {"Moans", new List<string>() { "Mmnh", "Nnngh", "Ahhhn", "Mhhn", "Ahhnn", "Nh", "Mhhnn", "Ahhnnn", "Nmhnn" }  },
    };

    /// <summary>
    /// Which type is currently set
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public string? SelectedType { get; set; }
}
