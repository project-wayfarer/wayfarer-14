using System;
using System.Collections.Generic;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._WF.Stylesheets;

public static class WFTraitStyles
{
    public static StyleRule[] GetRules(IResourceCache resCache)
    {
        var bgDark = Color.FromHex("#1a1a22");
        var bgMedium = Color.FromHex("#22222a");
        var bgLight = Color.FromHex("#2a2a35");
        var bgLighter = Color.FromHex("#32323e");
        var textPrimary = Color.FromHex("#e0e0e0");
        var textSecondary = Color.FromHex("#a0a0a0");
        var textMuted = Color.FromHex("#707070");
        var accentGreen = Color.FromHex("#4ade80");
        var accentYellow = Color.FromHex("#fbbf24");
        var accentRed = Color.FromHex("#f87171");
        var accentBlue = Color.FromHex("#60a5fa");

        var font10 = resCache.NotoStack(size: 10);
        var font11 = resCache.NotoStack(size: 11);
        var font12 = resCache.NotoStack(size: 12);
        var font14 = resCache.NotoStack(size: 14);

        var headerPanelBox = new StyleBoxFlat { BackgroundColor = bgLight, BorderColor = bgLighter, BorderThickness = new Thickness(0, 0, 0, 1) };
        headerPanelBox.SetContentMarginOverride(StyleBox.Margin.All, 0);

        var searchBarBox = new StyleBoxFlat { BackgroundColor = bgMedium };
        searchBarBox.SetContentMarginOverride(StyleBox.Margin.All, 0);

        var searchInputBox = new StyleBoxFlat { BackgroundColor = bgDark, ContentMarginLeftOverride = 8, ContentMarginRightOverride = 8 };

        var footerPanelBox = new StyleBoxFlat { BackgroundColor = bgMedium, BorderColor = bgLighter, BorderThickness = new Thickness(0, 1, 0, 0) };

        var categoryHeaderBox = new StyleBoxFlat { BackgroundColor = bgLight };
        categoryHeaderBox.SetContentMarginOverride(StyleBox.Margin.All, 0);

        var categoryHeaderButtonBox = new StyleBoxFlat { BackgroundColor = Color.Transparent };
        categoryHeaderButtonBox.SetContentMarginOverride(StyleBox.Margin.All, 0);

        var categoryContentBox = new StyleBoxFlat { BackgroundColor = bgMedium };
        var categoryAccentBox = new StyleBoxFlat { BackgroundColor = accentBlue };

        var entryPanelBox = new StyleBoxFlat { BackgroundColor = bgLight, BorderColor = bgLighter, BorderThickness = new Thickness(1) };
        entryPanelBox.SetContentMarginOverride(StyleBox.Margin.All, 0);

        var entrySelectedBox = new StyleBoxFlat { BackgroundColor = Color.FromHex("#2a3a4a"), BorderColor = accentBlue, BorderThickness = new Thickness(1) };
        entrySelectedBox.SetContentMarginOverride(StyleBox.Margin.All, 0);

        var entryDisabledBox = new StyleBoxFlat { BackgroundColor = bgDark, BorderColor = Color.FromHex("#2a2a2a"), BorderThickness = new Thickness(1) };
        entryDisabledBox.SetContentMarginOverride(StyleBox.Margin.All, 0);

        var progressBarBgBox = new StyleBoxFlat { BackgroundColor = bgDark, BorderColor = bgLighter, BorderThickness = new Thickness(1) };
        var progressBarFillFull = new StyleBoxFlat { BackgroundColor = accentGreen };
        var progressBarFillPartial = new StyleBoxFlat { BackgroundColor = accentYellow };
        var progressBarFillLow = new StyleBoxFlat { BackgroundColor = accentRed };
        var progressBarFillEmpty = new StyleBoxFlat { BackgroundColor = bgDark };

        StyleRule Panel(string cls, StyleBox box) =>
            new(new SelectorElement(typeof(PanelContainer), new[] { cls }, null, null),
                new[] { new StyleProperty(PanelContainer.StylePropertyPanel, box) });

        StyleRule Text(Type type, string cls, Font font, Color? color)
        {
            var props = new List<StyleProperty> { new(Label.StylePropertyFont, font) };
            if (color != null)
                props.Add(new StyleProperty(Label.StylePropertyFontColor, color.Value));
            return new StyleRule(new SelectorElement(type, new[] { cls }, null, null), props.ToArray());
        }

        return new[]
        {
            Panel("TraitsHeaderPanel", headerPanelBox),
            Text(typeof(Label), "TraitsTitleLabel", font14, textPrimary),
            Text(typeof(Label), "TraitsSubtitleLabel", font11, textSecondary),
            Text(typeof(Label), "TraitsStatLabel", font12, accentBlue),

            Panel("TraitsProgressBarBg", progressBarBgBox),
            Panel("TraitsProgressBarFill", progressBarFillFull),
            Panel("TraitsProgressBarFull", progressBarFillFull),
            Panel("TraitsProgressBarPartial", progressBarFillPartial),
            Panel("TraitsProgressBarLow", progressBarFillLow),
            Panel("TraitsProgressBarEmpty", progressBarFillEmpty),

            Panel("TraitsSearchBar", searchBarBox),
            new StyleRule(new SelectorElement(typeof(LineEdit), new[] { "TraitsSearchInput" }, null, null),
                new[] { new StyleProperty(LineEdit.StylePropertyStyleBox, searchInputBox) }),

            Panel("TraitsFooterPanel", footerPanelBox),
            Text(typeof(Label), "TraitsFooterText", font10, textMuted),

            Panel("TraitsCategoryHeader", categoryHeaderBox),
            new StyleRule(new SelectorElement(typeof(Button), new[] { "TraitsCategoryHeaderButton" }, null, null),
                new[] { new StyleProperty(ContainerButton.StylePropertyStyleBox, categoryHeaderButtonBox) }),
            Text(typeof(Label), "TraitsCategoryExpandIcon", font10, textSecondary),
            Text(typeof(Label), "TraitsCategoryNameLabel", font12, textPrimary),
            Text(typeof(Label), "TraitsCategoryStatsLabel", font10, textSecondary),
            Text(typeof(Label), "TraitsCategoryPointsLabel", font10, textMuted),

            Panel("TraitsCategoryAccent", categoryAccentBox),
            Panel("TraitsCategoryContent", categoryContentBox),

            Panel("TraitsEntryPanel", entryPanelBox),
            new StyleRule(new SelectorElement(typeof(PanelContainer), new[] { "TraitsEntryPanel", "TraitsEntrySelected" }, null, null),
                new[] { new StyleProperty(PanelContainer.StylePropertyPanel, entrySelectedBox) }),
            new StyleRule(new SelectorElement(typeof(PanelContainer), new[] { "TraitsEntryPanel", "TraitsEntryDisabled" }, null, null),
                new[]
                {
                    new StyleProperty(PanelContainer.StylePropertyPanel, entryDisabledBox),
                    new StyleProperty(Control.StylePropertyModulateSelf, new Color(1f, 1f, 1f, 0.5f)),
                }),
            Text(typeof(Label), "TraitsEntryNameLabel", font11, textPrimary),
            Text(typeof(Label), "TraitsEntryCostLabel", font11, null),
            Text(typeof(RichTextLabel), "TraitsEntryDescriptionLabel", font10, textSecondary),
        };
    }
}
