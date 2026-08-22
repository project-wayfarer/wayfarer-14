using Content.Client._Emberfall.Weapons.Ranged.Overlays;
using Content.Shared._Emberfall.Weapons.Ranged;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using System.Linq; // Wayfarer - Tracers
using Robust.Shared.Timing;
using System.Numerics; // Wayfarer - Tracers

namespace Content.Client._Emberfall.Weapons.Ranged.Systems;

public sealed class TracerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private Dictionary<TracerComponent, TracerData> _traces = new(); // Wayfarer - Tracers

    public override void Initialize()
    {
        base.Initialize();
        _overlay.AddOverlay(new TracerOverlay(this));

        SubscribeLocalEvent<TracerComponent, ComponentStartup>(OnTracerStart);
    }

    private void OnTracerStart(Entity<TracerComponent> ent, ref ComponentStartup args)
    {
        var xform = Transform(ent);
        var pos = _transform.GetWorldPosition(xform);

        _traces[ent.Comp] = new TracerData(pos, // Wayfarer - Tracers
            _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.Lifetime), // Wayfarer - Tracers
            xform.MapID // Wayfarer - Tracers
        );
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<TracerComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var tracer, out var xform))
        {
            var currentPos = _transform.GetWorldPosition(xform); // Wayfarer - Tracers

            if (currentPos.Equals(_traces[tracer].PositionHistory.Last())) // Wayfarer - Tracers
            {
                continue;
            }
            // Wayfarer - Tracers
            _traces[tracer].PositionHistory.Add(currentPos);
            _traces[tracer].EndTimes.Add(_timing.CurTime + TimeSpan.FromSeconds(tracer.Lifetime)); 

            while (_traces[tracer].PositionHistory.Count > 2 &&
                   GetTrailLength(_traces[tracer].PositionHistory) > tracer.Length)
            {
                _traces[tracer].PositionHistory.RemoveAt(0);
            }
            // End Wayfarer
        }

        // Wayfarer - Tracers
        // Clean up expired tracers
        foreach (var tracer in _traces)
        {
            if (tracer.Value.EndTimes.Last() < _timing.CurTime)
            {
                _traces.Remove(tracer.Key);
            }
        }
        // End Wayfarer
    }

    private static float GetTrailLength(List<Vector2> positions)
    {
        var length = 0f;
        for (var i = 1; i < positions.Count; i++)
        {
            length += Vector2.Distance(positions[i - 1], positions[i]);
        }
        return length;
    }

    public void Draw(DrawingHandleWorld handle, MapId currentMap)
    {
        foreach (var trace in _traces) // Wayfarer - Tracers
        {
            if (trace.Value.MapId != currentMap) // Wayfarer - Tracers
                continue;

            var positions = trace.Value.PositionHistory; // Wayfarer - Tracers
            var times = trace.Value.EndTimes; // Wayfarer - Tracers

            if (positions.Count < 2)
                continue;

            handle.SetTransform(Matrix3x2.Identity);

            for (var i = 1; i < positions.Count; i++)
            {
                //handle.DrawLine(positions[i - 1], positions[i], tracer.Color);
                // Wayfarer - Tracers
                // Fail fast
                if (times[i] < _timing.CurTime) continue;
                // Reduce opacity over time
                var amt = (times[i] - _timing.CurTime).TotalSeconds / trace.Key.Lifetime;
                var color = trace.Key.Color;
                color.A *= (float)amt;

                // Draw rect of width .05 from point to point
                var pt1 = positions[i];
                var pt2 = positions[i - 1];
                var tracerVector = Vector2.Create(pt2.X - pt1.X, pt2.Y - pt1.Y);
                var perp = Vector2.Create(tracerVector.Y, -tracerVector.X);
                perp.Normalize();
                perp *= .025f;

                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, new List<Vector2>() {
                    pt1 + perp,
                    pt2 + perp,
                    pt1 - perp,
                    pt2 - perp
                }, color);

            }
            // End Wayfarer
        }
    }
    // Wayfarer - Tracers
    public struct TracerData(Vector2 intialPosition, TimeSpan endTime, MapId mapId)
    {
        /// <summary>
        /// The history of positions this tracer has moved through
        /// </summary>
        public List<Vector2> PositionHistory = new List<Vector2>() { intialPosition };

        /// <summary>
        /// When this tracer effect should end
        /// </summary>
        public List<TimeSpan> EndTimes = new List<TimeSpan>() { endTime };

        public MapId MapId = mapId;
    }
    
}
