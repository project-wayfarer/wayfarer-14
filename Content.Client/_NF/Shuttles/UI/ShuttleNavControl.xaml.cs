using Content.Shared._NF.Shuttles.Events;
using Content.Shared.Shuttles.BUIStates;
using Robust.Shared.Physics.Components;
using System.Linq;
using System.Numerics;
using Content.Shared.Shuttles.Components;
using Robust.Client.Graphics;
using Robust.Shared.Collections;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Timing;
using Content.Shared._NF.Radar;
using Content.Client._NF.Radar;
using Content.Client.Station;
using Robust.Client.ResourceManagement;

// Purposefully colliding with base namespace.
namespace Content.Client.Shuttles.UI;

public sealed partial class ShuttleNavControl
{
    // Dependency
    private readonly StationSystem _station;
    private readonly RadarBlipSystem _blips;

    // Constants for gunnery system
    // These 2 handle timing updates
    private const float RadarUpdateInterval = 0f;
    private const float FireRateLimit = 0.1f; // 100ms between shots
    private static readonly Color TargetColor = Color.FromHex("#99ff66");
    // Wayfarer: inactive crew shuttles are gray and can be hidden at radar edge.
    private static readonly Color InactiveShuttleColor = Color.Gray;
    private float _updateAccumulator = 0f;
    
    // Edge detection for label hiding
    private const float EdgeMargin = 60f; // Distance from edge in pixels to be considered "near edge"

    private bool _isMouseDown;
    private bool _isMouseInside;
    private Vector2 _lastMousePos;
    private float _lastFireTime;

    // Constants for IFF system
    public float MaximumIFFDistance { get; set; } = -1f;
    public bool HideCoords { get; set; } = false;
    private static Color _dockLabelColor = Color.White;
    public bool HideTarget { get; set; } = false;
    public Vector2? Target { get; set; } = null;
    public NetEntity? TargetEntity { get; set; } = null;

    public InertiaDampeningMode DampeningMode { get; set; }
    public ServiceFlags ServiceFlags { get; set; } = ServiceFlags.None;

    public Font FontHeart;

    /// <summary>
    /// Updates the radar UI with the latest navigation state and sets additional NF-specific state.
    /// </summary>
    /// <param name="state">The navigation interface state.</param>
    private void NFUpdateState(NavInterfaceState state)
    {
        if (state.MaxIffRange != null)
            MaximumIFFDistance = state.MaxIffRange.Value;
        HideCoords = state.HideCoords;
        Target = state.Target;
        TargetEntity = state.TargetEntity;
        HideTarget = state.HideTarget;

        if (!EntManager.GetCoordinates(state.Coordinates).HasValue ||
            !EntManager.TryGetComponent(EntManager.GetCoordinates(state.Coordinates).GetValueOrDefault().EntityId, out TransformComponent? transform) ||
            !EntManager.HasComponent<PhysicsComponent>(transform.GridUid))
        {
            return;
        }

        DampeningMode = state.DampeningMode;
        ServiceFlags = state.ServiceFlags;
    }

    /// <summary>
    /// Checks if an IFF marker should be drawn based on distance and maximum IFF range.
    /// </summary>
    /// <param name="shouldDrawIff">Whether the IFF marker would otherwise be drawn.</param>
    /// <param name="distance">The distance vector to the object.</param>
    /// <returns>True if the IFF marker should be drawn, false otherwise.</returns>
    private bool NFCheckShouldDrawIffRangeCondition(bool shouldDrawIff, Vector2 distance)
    {
        if (shouldDrawIff && MaximumIFFDistance >= 0.0f)
        {
            if (distance.Length() > MaximumIFFDistance)
            {
                shouldDrawIff = false;
            }
        }
        return shouldDrawIff;
    }

    /// <summary>
    /// Adds a blip to the blip data list for later drawing.
    /// </summary>
    private static void NFAddBlipToList(List<BlipData> blipDataList, bool isOutsideRadarCircle, Vector2 uiPosition, int uiXCentre, int uiYCentre, Color color, string? shuttleName, EntityUid entityUid, float distance, bool isDistantPOI = false, bool isMouseOver = false, Vector2? gridMapCoords = null)
    {
        blipDataList.Add(new BlipData
        {
            IsOutsideRadarCircle = isOutsideRadarCircle,
            UiPosition = uiPosition,
            VectorToPosition = uiPosition - new Vector2(uiXCentre, uiYCentre),
            Color = color,
            ShuttleName = shuttleName,
            EntityUid = entityUid,
            Distance = distance,
            IsDistantPOI = isDistantPOI,
            IsMouseOver = isMouseOver,
            GridMapCoords = gridMapCoords ?? Vector2.Zero
        });
    }

    /// <summary>
    /// Groups nearby blips together to reduce clutter on the radar.
    /// </summary>
    private List<GroupedBlip> NFGroupBlips(List<BlipData> blipDataList, Vector2 mousePosition)
    {
        var grouped = new List<GroupedBlip>();
        var processed = new HashSet<BlipData>();

        foreach (var blip in blipDataList)
        {
            if (processed.Contains(blip))
                continue;

            var group = new GroupedBlip
            {
                Blips = new List<BlipData> { blip },
                UiPosition = blip.UiPosition,
                VectorToPosition = blip.VectorToPosition,
                Color = blip.Color,
                IsOutsideRadarCircle = blip.IsOutsideRadarCircle
            };

            processed.Add(blip);

            // Find nearby blips to group with
            foreach (var otherBlip in blipDataList)
            {
                if (processed.Contains(otherBlip))
                    continue;

                var distance = Vector2.Distance(blip.UiPosition * UIScale, otherBlip.UiPosition * UIScale);
                if (distance < BlipGroupDistance)
                {
                    group.Blips.Add(otherBlip);
                    processed.Add(otherBlip);
                }
            }

            // Calculate average position for the group
            if (group.Blips.Count > 1)
            {
                var avgPos = Vector2.Zero;
                var avgVec = Vector2.Zero;
                foreach (var groupBlip in group.Blips)
                {
                    avgPos += groupBlip.UiPosition;
                    avgVec += groupBlip.VectorToPosition;
                }
                group.UiPosition = avgPos / group.Blips.Count;
                group.VectorToPosition = avgVec / group.Blips.Count;
            }

            grouped.Add(group);
        }

        return grouped;
    }

    /// <summary>
    /// Adds blip style triangles that are on ships or pointing towards ships on the edges of the radar.
    /// Draws blips at the BlipData's uiPosition and uses VectorToPosition to rotate to point towards ships.
    /// Supports grouping of nearby shuttles to reduce clutter.
    /// </summary>
    private void NFDrawBlips(DrawingHandleScreen handle, List<BlipData> blipDataList)
    {
        var scaledMousePosition = GetMouseCoordinatesFromCenter().Position * UIScale;
        var groupedBlips = NFGroupBlips(blipDataList, scaledMousePosition);

        var blipValueList = new Dictionary<Color, ValueList<Vector2>>();

        foreach (var group in groupedBlips)
        {
            // Check if mouse is hovering over this group
            var isMouseOverGroup = Vector2.Distance(scaledMousePosition, group.UiPosition * UIScale) < 40f;
            
            // If it's a group and not hovered, draw one blip with a count and grouped label
            if (group.Blips.Count > 1 && !isMouseOverGroup)
            {
                // Wayfarer: If every blip in this collapsed group is an inactive edge blip, hide the group marker entirely.
                if (group.Blips.All(NFShouldSuppressInactiveEdgeBlip))
                    continue;

                var blipData = new BlipData
                {
                    IsOutsideRadarCircle = group.IsOutsideRadarCircle,
                    UiPosition = group.UiPosition,
                    VectorToPosition = group.VectorToPosition,
                    Color = group.Color
                };

                DrawSingleBlip(handle, blipData, blipValueList);

                // Draw count indicator
                var countText = $"×{group.Blips.Count}";
                var textDims = handle.GetDimensions(FontHeart, countText, 0.8f);
                var textPos = group.UiPosition * UIScale + new Vector2(RadarBlipSize * 0.7f, -RadarBlipSize * 0.5f);
                handle.DrawString(FontHeart, textPos, countText, 0.8f * UIScale, Color.White);
            }
            else
            {
                // Draw individual blips (either single or expanded group)
                foreach (var blip in group.Blips)
                {
                    // Draw labels for individual shuttles
                    if ((!blip.IsOutsideRadarCircle || blip.IsDistantPOI || blip.IsMouseOver || isMouseOverGroup) && blip.ShuttleName != null)
                    {
                        var blipSize = RadarBlipSize * 0.7f;
                        
                        // Check if blip is near edge
                        var isNearEdge = blip.UiPosition.X < EdgeMargin || blip.UiPosition.X > (Width - EdgeMargin) ||
                                         blip.UiPosition.Y < EdgeMargin || blip.UiPosition.Y > (Height - EdgeMargin);

                        // Wayfarer: optional suppression for inactive shuttle edge labels.
                        var suppressInactiveEdgeLabel = IgnoreEdgeInactiveShuttles &&
                                                         isNearEdge &&
                                                         NFIsInactiveShuttleLabel(blip.Color);
                        
                        // Only show label if not near edge (when hide edge labels is enabled), or if mouse is over the blip
                        if (!suppressInactiveEdgeLabel && (((!isNearEdge || !HideEdgeLabels) || blip.IsMouseOver || isMouseOverGroup)))
                        {
                            var displayedDistance = blip.Distance < 50f ? $"{blip.Distance:0.0}" : blip.Distance < 1000 ? $"{blip.Distance:0}" : $"{blip.Distance / 1000:0.0}k";
                            var labelText = Loc.GetString("shuttle-console-iff-label", ("name", blip.ShuttleName)!, ("distance", displayedDistance));
                            var fontScale = LabelFontSize / 10f; // Default font is 10, so scale accordingly
                            var labelDimensions = handle.GetDimensions(Font, labelText, fontScale);
                            var labelOffset = new Vector2()
                            {
                                X = blip.UiPosition.X > Width / 2f
                                    ? -labelDimensions.X - blipSize
                                    : blipSize,
                                Y = -labelDimensions.Y / 2f
                            };
                            
                            // Determine label opacity: 20% if in a group and not directly hovered, 100% if directly hovered
                            var labelColor = blip.Color;
                            if (group.Blips.Count > 1 && isMouseOverGroup && !blip.IsMouseOver)
                            {
                                labelColor = new Color(blip.Color.R, blip.Color.G, blip.Color.B, 0.2f);
                            }
                            
                            handle.DrawString(Font, (blip.UiPosition + labelOffset) * UIScale, labelText, fontScale * UIScale, labelColor);
                        }

                        // Draw coordinates on mouse over if enabled
                        if ((blip.IsMouseOver || isMouseOverGroup) && !HideCoords)
                        {
                            var coordsText = $"({blip.GridMapCoords.X:0.0}, {blip.GridMapCoords.Y:0.0})";
                            var coordDimensions = handle.GetDimensions(Font, coordsText, 0.7f);
                            var coordOffset = new Vector2()
                            {
                                X = blip.UiPosition.X > Width / 2f
                                    ? -coordDimensions.X - blipSize / 0.7f
                                    : blipSize,
                                Y = coordDimensions.Y / 2
                            };
                            handle.DrawString(Font, (blip.UiPosition + coordOffset) * UIScale, coordsText, 0.7f * UIScale, new Color(blip.Color.R * 0.8f, blip.Color.G * 0.8f, blip.Color.B * 0.8f, 0.5f));
                        }
                    }

                    // Determine blip opacity: 20% if in a group and not directly hovered, 100% if directly hovered
                    var blipColor = blip.Color;
                    if (group.Blips.Count > 1 && isMouseOverGroup && !blip.IsMouseOver)
                    {
                        blipColor = new Color(blip.Color.R, blip.Color.G, blip.Color.B, 0.2f);
                    }
                    
                    var blipDataWithOpacity = new BlipData
                    {
                        IsOutsideRadarCircle = blip.IsOutsideRadarCircle,
                        UiPosition = blip.UiPosition,
                        VectorToPosition = blip.VectorToPosition,
                        Color = blipColor,
                        ShuttleName = blip.ShuttleName,
                        EntityUid = blip.EntityUid,
                        Distance = blip.Distance,
                        IsDistantPOI = blip.IsDistantPOI,
                        IsMouseOver = blip.IsMouseOver,
                        GridMapCoords = blip.GridMapCoords
                    };

                    if (NFShouldSuppressInactiveEdgeBlip(blipDataWithOpacity))
                        continue;

                    DrawSingleBlip(handle, blipDataWithOpacity, blipValueList);
                }
            }
        }

        // One draw call for every color we have
        foreach (var color in blipValueList)
        {
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, color.Value.Span, color.Key);
        }
    }

    /// <summary>
    /// Draws a single blip triangle.
    /// </summary>
    private void DrawSingleBlip(DrawingHandleScreen handle, BlipData blipData, Dictionary<Color, ValueList<Vector2>> blipValueList)
    {
        var triangleShapeVectorPoints = new[]
        {
            new Vector2(0, 0),
            new Vector2(RadarBlipSize, 0),
            new Vector2(RadarBlipSize * 0.5f, RadarBlipSize)
        };

        if (blipData.IsOutsideRadarCircle)
        {
            // Calculate the angle of rotation
            var angle = (float)Math.Atan2(blipData.VectorToPosition.Y, blipData.VectorToPosition.X) + -1.6f;

            // Manually create a rotation matrix
            var cos = (float)Math.Cos(angle);
            var sin = (float)Math.Sin(angle);
            float[,] rotationMatrix = { { cos, -sin }, { sin, cos } };

            // Rotate each vertex
            for (var i = 0; i < triangleShapeVectorPoints.Length; i++)
            {
                var vertex = triangleShapeVectorPoints[i];
                var x = vertex.X * rotationMatrix[0, 0] + vertex.Y * rotationMatrix[0, 1];
                var y = vertex.X * rotationMatrix[1, 0] + vertex.Y * rotationMatrix[1, 1];
                triangleShapeVectorPoints[i] = new Vector2(x, y);
            }
        }

        var triangleCenterVector =
            (triangleShapeVectorPoints[0] + triangleShapeVectorPoints[1] + triangleShapeVectorPoints[2]) / 3;

        // Calculate the vectors from the center to each vertex
        var vectorsFromCenter = new Vector2[3];
        for (int i = 0; i < 3; i++)
        {
            vectorsFromCenter[i] = (triangleShapeVectorPoints[i] - triangleCenterVector) * UIScale;
        }

        // Calculate the vertices of the new triangle
        var newVerts = new Vector2[3];
        for (var i = 0; i < 3; i++)
        {
            newVerts[i] = (blipData.UiPosition * UIScale) + vectorsFromCenter[i];
        }

        if (!blipValueList.TryGetValue(blipData.Color, out var valueList))
        {
            valueList = new ValueList<Vector2>();
        }
        valueList.Add(newVerts[0]);
        valueList.Add(newVerts[1]);
        valueList.Add(newVerts[2]);
        blipValueList[blipData.Color] = valueList;
    }

    private void HandleMouseEntered(GUIMouseHoverEventArgs args)
    {
        _isMouseInside = true;
    }

    private static bool NFIsInactiveShuttleLabel(Color color)
    {
        const float epsilon = 0.01f;
        return Math.Abs(color.R - InactiveShuttleColor.R) < epsilon
               && Math.Abs(color.G - InactiveShuttleColor.G) < epsilon
               && Math.Abs(color.B - InactiveShuttleColor.B) < epsilon;
    }

    // Wayfarer: suppresses edge triangle rendering for inactive shuttles.
    private bool NFShouldSuppressInactiveEdgeBlip(BlipData blip)
    {
        return IgnoreEdgeInactiveShuttles
               && blip.IsOutsideRadarCircle
               && NFIsInactiveShuttleLabel(blip.Color);
    }

    private void HandleMouseExited(GUIMouseHoverEventArgs args)
    {
        _isMouseInside = false;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _isMouseDown = true;
        _lastMousePos = args.RelativePosition;
        TryFireAtPosition(args.RelativePosition);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        _updateAccumulator += args.DeltaSeconds;

        if (_updateAccumulator >= RadarUpdateInterval)
        {
            _updateAccumulator = 0; // I'm not subtracting because frame updates can majorly lag in a way normal ones cannot.

            if (_consoleEntity != null)
                _blips.RequestBlips((EntityUid)_consoleEntity);
        }

        if (_isMouseDown && _isMouseInside)
        {
            var currentTime = IoCManager.Resolve<IGameTiming>().CurTime.TotalSeconds;
            if (currentTime - _lastFireTime >= FireRateLimit)
            {
                var mousePos = UserInterfaceManager.MousePositionScaled;
                var relativePos = mousePos.Position - GlobalPosition;
                if (relativePos != _lastMousePos)
                {
                    _lastMousePos = relativePos;
                }
                TryFireAtPosition(relativePos);
                _lastFireTime = (float)currentTime;
            }
        }
    }
    private void TryFireAtPosition(Vector2 relativePosition)
    {
        if (_coordinates == null || _rotation == null || OnRadarClick == null)
            return;

        var a = InverseScalePosition(relativePosition);
        var relativeWorldPos = new Vector2(a.X, -a.Y);
        relativeWorldPos = _rotation.Value.RotateVec(relativeWorldPos);
        var coords = _coordinates.Value.Offset(relativeWorldPos);
        OnRadarClick?.Invoke(coords);
    }

    private void DrawBlipShape(DrawingHandleScreen handle, Vector2 position, float size, Color color, RadarBlipShape shape)
    {
        switch (shape)
        {
            case RadarBlipShape.Circle:
                handle.DrawCircle(position, size, color);
                break;
            case RadarBlipShape.Square:
                var halfSize = size / 2;
                var rect = new UIBox2(
                    position.X - halfSize,
                    position.Y - halfSize,
                    position.X + halfSize,
                    position.Y + halfSize
                );
                handle.DrawRect(rect, color);
                break;
            case RadarBlipShape.Triangle:
                var points = new Vector2[]
                {
                position + new Vector2(0, -size),
                position + new Vector2(-size * 0.866f, size * 0.5f),
                position + new Vector2(size * 0.866f, size * 0.5f)
                };
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, points, color);
                break;
            case RadarBlipShape.Star:
                DrawStar(handle, position, size, color);
                break;
            case RadarBlipShape.Diamond:
                var diamondPoints = new Vector2[]
                {
                position + new Vector2(0, -size),
                position + new Vector2(size, 0),
                position + new Vector2(0, size),
                position + new Vector2(-size, 0)
                };
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, diamondPoints, color);
                break;
            case RadarBlipShape.Hexagon:
                DrawHexagon(handle, position, size, color);
                break;
            case RadarBlipShape.Arrow:
                DrawArrow(handle, position, size, color);
                break;
            case RadarBlipShape.Heart:
                handle.DrawString(
                    FontHeart,
                    position,
                    "♥",
                    size * 0.5f,
                    color);
                break;
            case RadarBlipShape.X:
                var xPoints = new Vector2[]
                {
                    position + new Vector2(-size, -size),
                    position + new Vector2(size, size),
                    position + new Vector2(size, -size),
                    position + new Vector2(-size, size)
                };
                handle.DrawPrimitives(DrawPrimitiveTopology.LineList, xPoints, color);
                break;
        }
    }

    private void DrawStar(DrawingHandleScreen handle, Vector2 position, float size, Color color)
    {
        const int points = 5;
        const float innerRatio = 0.4f;
        var vertices = new Vector2[points * 2 + 2]; // outer and inner point, five times, plus a center point and the original drawn point

        vertices[0] = position;
        for (var i = 0; i <= points * 2; i++)
        {
            var angle = i * Math.PI / points;
            var radius = i % 2 == 0 ? size : size * innerRatio;
            vertices[i + 1] = position + new Vector2(
                (float)Math.Sin(angle) * radius,
                -(float)Math.Cos(angle) * radius
            );
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, vertices, color);
    }

    private void DrawHexagon(DrawingHandleScreen handle, Vector2 position, float size, Color color)
    {
        var vertices = new Vector2[6];
        for (var i = 0; i < 6; i++)
        {
            var angle = i * Math.PI / 3;
            vertices[i] = position + new Vector2(
                (float)Math.Sin(angle) * size,
                -(float)Math.Cos(angle) * size
            );
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, vertices, color);
    }

    private void DrawArrow(DrawingHandleScreen handle, Vector2 position, float size, Color color)
    {
        var vertices = new Vector2[]
        {
        position + new Vector2(0, -size),           // Tip
        position + new Vector2(-size * 0.5f, 0),    // Left wing
        position + new Vector2(0, size * 0.5f),     // Bottom
        position + new Vector2(size * 0.5f, 0)      // Right wing
        };

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, vertices, color);
    }
}
