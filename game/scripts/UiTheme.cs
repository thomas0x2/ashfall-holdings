using Godot;

namespace Ashfall.Game;

/// <summary>One shared theme so every panel, button, and bar looks like it belongs to the same painting.</summary>
public static class UiTheme
{
    public static Theme Create()
    {
        var t = new Theme { DefaultFontSize = 14 };

        t.SetStylebox("panel", "PanelContainer", Box(Palette.UiPanel, Palette.UiBorder, 2, 10));
        t.SetStylebox("panel", "TooltipPanel", Box(new Color(0.10f, 0.08f, 0.06f, 0.97f), Palette.UiBorder, 1, 8));
        t.SetColor("font_color", "TooltipLabel", Palette.UiText);

        t.SetStylebox("normal", "Button", Box(Palette.UiButton, Palette.UiBorder with { A = 0.5f }, 1, 6));
        t.SetStylebox("hover", "Button", Box(Palette.UiButtonHover, Palette.UiBorder, 1, 6));
        t.SetStylebox("pressed", "Button", Box(Palette.UiButtonPressed, Palette.UiAccent, 2, 6));
        t.SetStylebox("hover_pressed", "Button", Box(Palette.UiButtonPressed, Palette.UiAccent, 2, 6));
        t.SetStylebox("disabled", "Button", Box(Palette.UiButton with { A = 0.5f }, Palette.UiBorder with { A = 0.2f }, 1, 6));
        t.SetStylebox("focus", "Button", new StyleBoxEmpty());
        t.SetColor("font_color", "Button", Palette.UiText);
        t.SetColor("font_hover_color", "Button", Palette.UiText);
        t.SetColor("font_pressed_color", "Button", Palette.UiText);
        t.SetColor("font_hover_pressed_color", "Button", Palette.UiText);
        t.SetColor("font_disabled_color", "Button", Palette.UiMuted with { A = 0.6f });

        t.SetColor("font_color", "Label", Palette.UiText);
        t.SetColor("default_color", "RichTextLabel", Palette.UiText);
        t.SetFontSize("normal_font_size", "RichTextLabel", 13);

        t.SetStylebox("separator", "HSeparator", new StyleBoxLine { Color = Palette.UiBorder with { A = 0.4f }, Thickness = 1 });
        return t;
    }

    public static StyleBoxFlat Box(Color bg, Color border, int borderWidth, int margin)
    {
        var sb = new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border,
            ContentMarginLeft = margin,
            ContentMarginRight = margin,
            ContentMarginTop = margin * 0.7f,
            ContentMarginBottom = margin * 0.7f,
        };
        sb.SetBorderWidthAll(borderWidth);
        sb.SetCornerRadiusAll(4);
        return sb;
    }

    public static Label Text(string text = "", int size = 14, Color? color = null)
    {
        var l = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        if (size != 14) l.AddThemeFontSizeOverride("font_size", size);
        if (color is { } c) l.AddThemeColorOverride("font_color", c);
        return l;
    }

    public static ProgressBar Bar(Color fill, float width = 70, float height = 8, double max = 100)
    {
        var bar = new ProgressBar
        {
            ShowPercentage = false,
            MinValue = 0,
            MaxValue = max,
            CustomMinimumSize = new Vector2(width, height),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var bg = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.35f) };
        bg.SetCornerRadiusAll(3);
        var fg = new StyleBoxFlat { BgColor = fill };
        fg.SetCornerRadiusAll(3);
        bar.AddThemeStyleboxOverride("background", bg);
        bar.AddThemeStyleboxOverride("fill", fg);
        return bar;
    }

    public static Button Button(string text, string tooltip = "")
    {
        return new Button { Text = text, TooltipText = tooltip, FocusMode = Control.FocusModeEnum.None };
    }

    /// <summary>Make a control and everything under it ignore the mouse, so a parent button gets the click.</summary>
    public static void PassMouse(Control c)
    {
        c.MouseFilter = Control.MouseFilterEnum.Ignore;
        foreach (var child in c.GetChildren())
            if (child is Control cc) PassMouse(cc);
    }
}
