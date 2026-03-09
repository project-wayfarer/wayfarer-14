using System.Numerics;

namespace Content.Server.Shuttles.Components
{
    [RegisterComponent]
    public sealed partial class ShuttleComponent : Component
    {
        [ViewVariables]
        public bool Enabled = true;

        [ViewVariables]
        public Vector2[] CenterOfThrust = new Vector2[4];

        /// <summary>
        /// Thrust gets multiplied by this value if it's for braking.
        /// </summary>
        public const float BrakeCoefficient = 1.5f;

        /// <summary>
        /// Maximum velocity.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public float BaseMaxLinearVelocity = 23.07f;  //Frontier 60 - 23.07. Upstream has it set to 60 to test and for collisions currently. Also, for some reason this value is increased by 30%, not sure if parts related or otherwise, so we do a 23.07 to reach a tier 1 velocity of 30.

        public const float MaxAngularVelocity = 4f;

        /// <summary>
        /// The cached thrust available for each cardinal direction
        /// </summary>
        [ViewVariables]
        public readonly float[] LinearThrust = new float[4];

        /// <summary>
        /// The cached thrust available for each cardinal direction, if all thrusters are T1
        /// </summary>
        [ViewVariables]
        public readonly float[] BaseLinearThrust = new float[4];

        /// <summary>
        /// The thrusters contributing to each direction for impulse.
        /// </summary>
        // No touchy
        public readonly List<EntityUid>[] LinearThrusters = new List<EntityUid>[]
        {
            new(),
            new(),
            new(),
            new(),
        };

        /// <summary>
        /// The thrusters contributing to the angular impulse of the shuttle.
        /// </summary>
        public readonly List<EntityUid> AngularThrusters = new();

        [ViewVariables]
        public float AngularThrust = 0f;

        /// <summary>
        /// A bitmask of all the directions we are considered thrusting.
        /// </summary>
        [ViewVariables]
        public DirectionFlag ThrustDirections = DirectionFlag.None;

        // Wayfarer start: Remove 0.0 sentinel value for FTL
        /// <summary>
        /// Damping modifier applied to the shuttle's physics component.
        /// </summary>
        [DataField]
        public float DampingModifier;
        // Wayfarer end

        /// <summary>
        /// Delay between checks to throw on the E-brake.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite), DataField("brakeDelay")]
        public TimeSpan BrakeDelay = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Next time we should check to throw on the E-brake.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite), DataField("nextBrakeCheck")]
        public TimeSpan NextBrakeCheck = TimeSpan.Zero;

        /// <summary>
        /// E-Brake is currently active.
        /// </summary>
        public bool EBrakeActive = false;

        /// <summary>
        /// Its a player shuttle!
        /// </summary>
        public bool PlayerShuttle = false;
    }
}
