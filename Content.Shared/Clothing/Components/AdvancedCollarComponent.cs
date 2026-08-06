using Content.Shared.Clothing.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Containers;
using Robust.Shared.Audio;

namespace Content.Shared.Clothing.Components;

/// <summary>
/// Component for collars that can have modules installed into them.
/// </summary>
[RegisterComponent]
[NetworkedComponent]
[Access(typeof(AdvancedCollarSystem))]
public sealed partial class AdvancedCollarComponent : Component
{
    /// <summary>
    /// Container holding the installed modules.
    /// </summary>
    [ViewVariables]
    public Container ModuleContainer = null!;

    /// <summary>
    /// Maximum number of modules that can be installed.
    /// </summary>
    [DataField]
    public int MaxModules = 5; // WF more modules by default

    /// <summary>
    /// Sound played when a module is extracted.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("moduleExtractionSound")]
    public SoundSpecifier ModuleExtractionSound = new SoundPathSpecifier("/Audio/Items/pistol_magout.ogg");

    /// <summary>
    /// Sound played when a module is inserted.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("moduleInsertionSound")]
    public SoundSpecifier ModuleInsertionSound = new SoundPathSpecifier("/Audio/Items/pistol_magin.ogg");
}
