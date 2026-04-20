using Content.Shared.Gravity;
using Content.Shared.Maps;
using Content.Shared.NPC;
using Robust.Shared.Map.Components;
using Robust.Shared.Spawners;

namespace Content.Server.NPC.Pathfinding;

public sealed partial class PathfindingSystem
{
    /*
     * Code that is common to all pathfinding methods.
     */

    /// <summary>
    /// Maximum amount of nodes we're allowed to expand.
    /// </summary>
    private const int NodeLimit = 512;

    private sealed class PathComparer : IComparer<ValueTuple<float, PathPoly>>
    {
        public int Compare((float, PathPoly) x, (float, PathPoly) y)
        {
            return y.Item1.CompareTo(x.Item1);
        }
    }

    private static readonly PathComparer PathPolyComparer = new();

    private List<PathPoly> ReconstructPath(Dictionary<PathPoly, PathPoly> path, PathPoly currentNodeRef)
    {
        var running = new List<PathPoly> { currentNodeRef };
        while (path.ContainsKey(currentNodeRef))
        {
            var previousCurrent = currentNodeRef;
            currentNodeRef = path[currentNodeRef];
            path.Remove(previousCurrent);
            running.Add(currentNodeRef);
        }

        running.Reverse();
        return running;
    }

    private float GetTileCost(PathRequest request, PathPoly start, PathPoly end)
    {
        var modifier = 1f;

        // TODO
        if ((end.Data.Flags & PathfindingBreadcrumbFlag.Space) != 0x0)
        {
            return 0f;
        }

        if ((request.CollisionLayer & end.Data.CollisionMask) != 0x0 ||
            (request.CollisionMask & end.Data.CollisionLayer) != 0x0)
        {
            var isDoor = (end.Data.Flags & PathfindingBreadcrumbFlag.Door) != 0x0;
            var isAccess = (end.Data.Flags & PathfindingBreadcrumbFlag.Access) != 0x0;
            var isClimb = (end.Data.Flags & PathfindingBreadcrumbFlag.Climb) != 0x0;

            // TODO: Handling power + door prying
            // Door we should be able to open
            if (isDoor && !isAccess && (request.Flags & PathFlags.Interact) != 0x0)
            {
                modifier += 0.5f;
            }
            // Door we can force open one way or another
            else if (isDoor && isAccess && (request.Flags & PathFlags.Prying) != 0x0)
            {
                modifier += 10f;
            }
            else if ((request.Flags & PathFlags.Smashing) != 0x0 && end.Data.Damage > 0f)
            {
                modifier += 30f + end.Data.Damage / 100f; // Wayfarer - Massively increase NavSmash pathfinding cost
            }
            else if (isClimb && (request.Flags & PathFlags.Climbing) != 0x0)
            {
                modifier += 0.5f;
            }
            else
            {
                return 0f;
            }
        }

        return modifier * OctileDistance(end, start);
    }

    #region Simplifier

    public List<PathPoly> Simplify(List<PathPoly> vertices, float tolerance = 0)
    {
        // #Misfits Change /Fix/: Keep exact obstacle nodes, but collapse free-space staircase
        // segments into fewer waypoints so steering stops overreacting to every intermediate turn.
        if (vertices.Count <= 2)
            return vertices;

        var simplified = new List<PathPoly>();

        for (var i = 0; i < vertices.Count; i++)
        {
            // No wraparound for negative sooooo
            var prev = vertices[i == 0 ? vertices.Count - 1 : i - 1];
            var current = vertices[i];
            var next = vertices[(i + 1) % vertices.Count];

            var prevData = prev.Data;
            var currentData = current.Data;
            var nextData = next.Data;

            // If they collinear, continue
            if (i != 0 && i != vertices.Count - 1 &&
                prevData.Equals(currentData) &&
                currentData.Equals(nextData) &&
                IsCollinear(prev, current, next, tolerance))
            {
                continue;
            }

            simplified.Add(current);
        }

        // Farseer didn't seem to handle straight lines and nuked all points
        if (simplified.Count == 0)
        {
            simplified.Add(vertices[0]);
            simplified.Add(vertices[^1]);
        }

        if (simplified.Count <= 2)
            return simplified;

        var shortcut = new List<PathPoly> { simplified[0] };
        var anchorIndex = 0;

        while (anchorIndex < simplified.Count - 1)
        {
            var nextIndex = anchorIndex + 1;

            for (var candidateIndex = simplified.Count - 1; candidateIndex > anchorIndex + 1; candidateIndex--)
            {
                if (!CanShortcutFreeSpace(simplified, anchorIndex, candidateIndex, tolerance))
                    continue;

                nextIndex = candidateIndex;
                break;
            }

            shortcut.Add(simplified[nextIndex]);
            anchorIndex = nextIndex;
        }

        // Check LOS and cut out more nodes
        // TODO: Grid cast
        // https://github.com/recastnavigation/recastnavigation/blob/c5cbd53024c8a9d8d097a4371215e3342d2fdc87/Detour/Source/DetourNavMeshQuery.cpp#L2455
        // Essentially you just do a raycast but a specialised version.

        return shortcut;
    }

    private bool CanShortcutFreeSpace(List<PathPoly> vertices, int anchorIndex, int candidateIndex, float tolerance)
    {
        var start = vertices[anchorIndex];
        var end = vertices[candidateIndex];

        if (!start.IsValid() || !end.IsValid() || start.GraphUid != end.GraphUid)
            return false;

        if (!start.Data.IsFreeSpace || !end.Data.IsFreeSpace)
            return false;

        var startPoint = start.Box.Center;
        var endPoint = end.Box.Center;

        for (var i = anchorIndex + 1; i < candidateIndex; i++)
        {
            var node = vertices[i];

            if (!node.IsValid() || node.GraphUid != start.GraphUid || !node.Data.IsFreeSpace)
                return false;

            // Keep the shortcut conservative: the direct segment must still pass through every
            // intermediate free-space poly, so we smooth diagonal staircases without cutting corners.
            if (!SegmentIntersectsBox(startPoint, endPoint, node.Box.Enlarged(tolerance + 0.05f)))
                return false;
        }

        return true;
    }

    private bool SegmentIntersectsBox(System.Numerics.Vector2 start, System.Numerics.Vector2 end, Box2 box)
    {
        if (box.Contains(start) || box.Contains(end))
            return true;

        var direction = end - start;
        var min = 0f;
        var max = 1f;

        if (!ClipAxis(start.X, direction.X, box.Left, box.Right, ref min, ref max))
            return false;

        if (!ClipAxis(start.Y, direction.Y, box.Bottom, box.Top, ref min, ref max))
            return false;

        return max >= min;
    }

    private bool ClipAxis(float start, float direction, float minBound, float maxBound, ref float min, ref float max)
    {
        if (Math.Abs(direction) < 0.0001f)
            return start >= minBound && start <= maxBound;

        var inv = 1f / direction;
        var enter = (minBound - start) * inv;
        var exit = (maxBound - start) * inv;

        if (enter > exit)
            (enter, exit) = (exit, enter);

        min = Math.Max(min, enter);
        max = Math.Min(max, exit);
        return max >= min;
    }

    private bool IsCollinear(PathPoly prev, PathPoly current, PathPoly next, float tolerance)
    {
        return FloatInRange(Area(prev, current, next), -tolerance, tolerance);
    }

    private float Area(PathPoly a, PathPoly b, PathPoly c)
    {
        var (ax, ay) = a.Box.Center;
        var (bx, by) = b.Box.Center;
        var (cx, cy) = c.Box.Center;

        return ax * (by - cy) + bx * (cy - ay) + cx * (ay - by);
    }

    private bool FloatInRange(float value, float min, float max)
    {
        return (value >= min && value <= max);
    }

    #endregion
}
