using Content.Server.Explosion.EntitySystems;
using Content.Shared.DeviceLinking;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.DeviceLinking.Components;

/// <summary>
/// Will send a signal to a linked network port upon a <see cref="TriggerEvent"/>.
/// </summary>
[RegisterComponent]
public sealed partial class SignalOnTriggerComponent : Component
{
    /// <summary>
    ///     The port that gets signaled when triggered.
    /// </summary>
    [DataField("triggerPort", customTypeSerializer: typeof(PrototypeIdSerializer<SourcePortPrototype>))]
    public string TriggerPort = "SignalTrigger";
}
