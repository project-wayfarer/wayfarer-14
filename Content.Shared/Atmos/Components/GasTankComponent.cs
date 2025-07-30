using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Atmos.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class GasTankComponent : Component, IGasMixtureHolder
{
    public const float MaxExplosionRange = 26f;
    private const float DefaultLowPressure = 0f;
    private const float DefaultOutputPressure = Atmospherics.OneAtmosphere;

    public int Integrity = 3;
    public bool IsLowPressure => Air.Pressure <= TankLowPressure;

    [DataField]
    public SoundSpecifier RuptureSound = new SoundPathSpecifier("/Audio/Effects/spray.ogg");

    [DataField]
    public SoundSpecifier? ConnectSound =
        new SoundPathSpecifier("/Audio/Effects/internals.ogg")
        {
            Params = AudioParams.Default.WithVolume(5f),
        };

    [DataField]
    public SoundSpecifier? DisconnectSound;

    // Cancel toggles sounds if we re-toggle again.

    public EntityUid? ConnectStream;
    public EntityUid? DisconnectStream;

    [DataField]
    public GasMixture Air { get; set; } = new();

    /// <summary>
    ///     Pressure at which tank should be considered 'low' such as for internals.
    /// </summary>
    [DataField]
    public float TankLowPressure = DefaultLowPressure;

    /// <summary>
    ///     Distributed pressure.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float OutputPressure = DefaultOutputPressure;

    /// <summary>
    ///     The maximum allowed output pressure.
    /// </summary>
    [DataField]
    public float MaxOutputPressure = 3 * DefaultOutputPressure;

    /// <summary>
    ///     Tank is connected to internals.
    /// </summary>
    [ViewVariables]
    public bool IsConnected => User != null;

    [DataField, AutoNetworkedField]
    public EntityUid? User;

    /// <summary>
    ///     True if this entity was recently moved out of a container. This might have been a hand -> inventory
    ///     transfer, or it might have been the user dropping the tank. This indicates the tank needs to be checked.
    /// </summary>
    [ViewVariables]
    public bool CheckUser;

    /// <summary>
    ///     Pressure at which tanks start leaking.
    /// </summary>
    [DataField]
    public float TankLeakPressure = 30 * Atmospherics.OneAtmosphere;

    /// <summary>
    ///     Pressure at which tank spills all contents into atmosphere.
    /// </summary>
    [DataField]
    public float TankRupturePressure = 40 * Atmospherics.OneAtmosphere;

    /// <summary>
    ///     Base 3x3 explosion.
    /// </summary>
    [DataField]
    public float TankFragmentPressure = 50 * Atmospherics.OneAtmosphere;

    /// <summary>
    ///     Increases explosion for each scale kPa above threshold.
    /// </summary>
    [DataField]
    public float TankFragmentScale = 2.25f * Atmospherics.OneAtmosphere;

    [DataField]
    public EntProtoId ToggleAction = "ActionToggleInternals";

    [DataField, AutoNetworkedField]
    public EntityUid? ToggleActionEntity;

    /// <summary>
    ///     Valve to release gas from tank
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsValveOpen;

    /// <summary>
    ///     Gas release rate in L/s
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ValveOutputRate = 100f;

    [DataField]
    public SoundSpecifier ValveSound =
        new SoundCollectionSpecifier("valveSqueak")
        {
            Params = AudioParams.Default.WithVolume(-5f),
        };

    // COYOTE START: Added pressure beep warning system thing
    /// <summary>
    /// This thing can alert the user when the tank is low on pressure!
    /// This is a list of those alert threshold classes!
    /// try to keep them in order of percentage, lowest to highest.
    /// The first one is the most critical, and the last one is the least critical.
    /// </summary>
    public List<GasPressureAlertThreshold> AlertThresholds = new()
        {
            new GasPressureAlertThreshold(
                0.10f,
                new SoundPathSpecifier("/Audio/_COYOTE/GasWarnings/airwarning_critical.ogg"),
                new SoundPathSpecifier("/Audio/_COYOTE/GasWarnings/jetpack_critical.ogg")),
            new GasPressureAlertThreshold(
                0.20f,
                new SoundPathSpecifier("/Audio/_COYOTE/GasWarnings/airwarning_verylow.ogg"),
                new SoundPathSpecifier("/Audio/_COYOTE/GasWarnings/jetpack_verylow.ogg")),
            new GasPressureAlertThreshold(
                0.35f,
                new SoundPathSpecifier("/Audio/_COYOTE/GasWarnings/airwarning_low.ogg"),
                new SoundPathSpecifier("/Audio/_COYOTE/GasWarnings/jetpack_low.ogg")),
        };

    /// <summary>
    /// Turn that damn noise off!
    /// </summary>
    public bool HushAlerts = false;

    /// <summary>
    /// A threshold for the gas tank to be considered "low pressure" for internals.
    /// </summary>
    [Serializable]
    public sealed class GasPressureAlertThreshold
    {
        /// <summary>
        /// The pressure threshold for the alert.
        /// </summary>
        public float PressurePercentThreshold = 0.25f;

        /// <summary>
        /// Has this alert been tripped?
        /// </summary>
        public bool Tripped = false;

        /// <summary>
        /// The sound to play when the alert is tripped.
        /// </summary>
        public SoundSpecifier AlertSound = new SoundPathSpecifier("/Audio/_COYOTE/GasWarnings/airwarning_low.ogg");

        /// <summary>
        /// The sound to play when the alert is tripped,
        /// And is an active jetpack, and is not internals.
        /// yeah pretty specific but, ya know how it is.
        /// </summary>
        public SoundSpecifier JetpackAlertSound = new SoundPathSpecifier("/Audio/_COYOTE/GasWarnings/jetpack_low.ogg");

        public GasPressureAlertThreshold(float pressurePercentThreshold,
            SoundSpecifier alertSound,
            SoundSpecifier jetpackAlertSound)
        {
            PressurePercentThreshold = pressurePercentThreshold;
            AlertSound = alertSound;
            JetpackAlertSound = jetpackAlertSound;
        }
    }
    // COYOTE END
}
