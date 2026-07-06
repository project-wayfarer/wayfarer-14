using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._WF.Corporations;

/// <summary>
/// Defines entity prototypes and tags whose entities are deleted from corporation player stations before saving.
/// Edit <c>Resources/Prototypes/_WF/corpStationSaveBlacklist.yml</c> to maintain the lists.
/// </summary>
[Prototype("corpStationSaveBlacklist")]
public sealed class CorpStationSaveBlacklistPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Entity prototype IDs whose entities will be deleted from the station grid before saving.
    /// </summary>
    [DataField]
    public List<EntProtoId> Prototypes = new();

    /// <summary>
    /// Tag IDs — any entity that has one of these tags will be deleted from the station grid before saving.
    /// </summary>
    [DataField]
    public List<ProtoId<TagPrototype>> Tags = new();
}
