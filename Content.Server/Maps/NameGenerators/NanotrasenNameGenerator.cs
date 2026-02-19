using JetBrains.Annotations;
using Robust.Shared.Random;

namespace Content.Server.Maps.NameGenerators;

[UsedImplicitly]
public sealed partial class NanotrasenNameGenerator : StationNameGenerator
{
    /// <summary>
    ///     Where the map comes from. Should be a two or three letter code, for example "VG" for Packedstation.
    /// </summary>
    [DataField("prefixCreator")] public string PrefixCreator = default!;

    private string Prefix => "NT";

    public override string FormatName(string input)
    {
        var random = IoCManager.Resolve<IRobustRandom>();

        // No way in hell am I writing custom format code just to add nice names. You can live with {0}
        return string.Format(input, $"{Prefix}{PrefixCreator}", $"-{random.Next(0, 1000):D3}"); // #WF remove the random codes for our ships to use identifiers
    }
}
