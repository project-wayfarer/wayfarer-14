using System.Numerics;
using Content.Client._WF.Shuttles;
using Robust.Client.Graphics;

namespace Content.Client.Shuttles.UI;

public sealed partial class ShuttleNavControl
{
    private ShipValueSystem? _shipValues;

    private ShipValueSystem ShipValues => _shipValues ??= EntManager.System<ShipValueSystem>();

    private readonly HashSet<EntityUid> _gridsInView = new();
    private uint _gridsInViewFrame;

    private bool IsGridInView(EntityUid grid)
    {
        return _gridsInViewFrame == Timing.CurFrame && _gridsInView.Contains(grid);
    }

    private void DrawShipValue(DrawingHandleScreen handle, BlipData blip, Vector2 labelOffset, Vector2 labelDimensions, float fontScale, float blipSize)
    {
        if (!IsGridInView(blip.EntityUid))
            return;

        var y = labelOffset.Y + labelDimensions.Y;

        if (ShipValues.TryGetLabel(blip.EntityUid, out var label))
            y += DrawValueLine(handle, blip, label, y, fontScale, blipSize);

        if (ShipValues.TryGetDcLabel(blip.EntityUid, out var dcLabel))
            DrawValueLine(handle, blip, dcLabel, y, fontScale, blipSize);
    }

    private float DrawValueLine(DrawingHandleScreen handle, BlipData blip, string label, float y, float fontScale, float blipSize)
    {
        var valueDimensions = handle.GetDimensions(Font, label, fontScale);

        var valueOffset = new Vector2()
        {
            X = blip.UiPosition.X > Width / 2f
                ? -valueDimensions.X - blipSize
                : blipSize,
            Y = y
        };

        handle.DrawString(Font, (blip.UiPosition + valueOffset) * UIScale, label, fontScale * UIScale, ShipValueSystem.LabelColor);
        return valueDimensions.Y;
    }

    private void DrawHullShipValue(DrawingHandleScreen handle, EntityUid grid, Vector2 localCenter, Matrix3x2 gridToView, bool hasBlip)
    {
        if (_gridsInViewFrame != Timing.CurFrame)
        {
            _gridsInView.Clear();
            _gridsInViewFrame = Timing.CurFrame;
        }

        _gridsInView.Add(grid);

        if (hasBlip)
            return;

        var hasValue = ShipValues.TryGetLabel(grid, out var label);
        var hasDc = ShipValues.TryGetDcLabel(grid, out var dcLabel);
        if (!hasValue && !hasDc)
            return;

        var fontScale = LabelFontSize / 10f;
        var position = Vector2.Transform(localCenter, gridToView);

        if (hasValue)
            position.Y += DrawHullLine(handle, label!, position, fontScale);

        if (hasDc)
            DrawHullLine(handle, dcLabel!, position, fontScale);
    }

    private float DrawHullLine(DrawingHandleScreen handle, string label, Vector2 position, float fontScale)
    {
        var dimensions = handle.GetDimensions(Font, label, fontScale * UIScale);
        handle.DrawString(Font, position - new Vector2(dimensions.X / 2f, 0f), label, fontScale * UIScale, ShipValueSystem.LabelColor);
        return dimensions.Y;
    }

    private float ShipValueCoordsShift(DrawingHandleScreen handle, BlipData blip, bool labelSuppressed)
    {
        if (labelSuppressed || !IsGridInView(blip.EntityUid))
            return 0f;

        var shift = 0f;

        if (ShipValues.TryGetLabel(blip.EntityUid, out var label))
            shift += handle.GetDimensions(Font, label, LabelFontSize / 10f).Y;

        if (ShipValues.TryGetDcLabel(blip.EntityUid, out var dcLabel))
            shift += handle.GetDimensions(Font, dcLabel, LabelFontSize / 10f).Y;

        return shift;
    }
}
