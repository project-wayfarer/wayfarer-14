using System;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Input;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client.Guidebook.RichText;

/// <summary>
/// Markup tag for creating clickable links to external websites.
/// Usage: [extlink="Click here" link="https://example.com"]
/// </summary>
[UsedImplicitly]
public sealed class ExternalLinkTag : IMarkupTag
{
    [Dependency] private readonly IUriOpener _uriOpener = default!;

    public static Color LinkColor => Color.CornflowerBlue;

    public string Name => "extlink";

    /// <inheritdoc/>
    public bool TryGetControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        if (!node.Value.TryGetString(out var text)
            || !node.Attributes.TryGetValue("link", out var linkParameter)
            || !linkParameter.TryGetString(out var link))
        {
            control = null;
            return false;
        }

        // Simple validation - just check if it starts with http:// or https://
        if (!link.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !link.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Warning($"extlink tag only supports http/https URLs, got: {link}");
            control = null;
            return false;
        }

        var label = new Label();
        label.Text = text;

        label.MouseFilter = Control.MouseFilterMode.Stop;
        label.FontColorOverride = LinkColor;
        label.DefaultCursorShape = Control.CursorShape.Hand;

        // Add visual feedback on hover
        label.OnMouseEntered += _ => label.FontColorOverride = Color.LightSkyBlue;
        label.OnMouseExited += _ => label.FontColorOverride = LinkColor;
        label.OnKeyBindDown += args => OnKeybindDown(args, link);

        control = label;
        return true;
    }

    private void OnKeybindDown(GUIBoundKeyEventArgs args, string link)
    {
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        try
        {
            _uriOpener.OpenUri(link);
            Logger.Info($"Opening external link: {link}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to open external link {link}: {ex.Message}");
        }
    }
}
