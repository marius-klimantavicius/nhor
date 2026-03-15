using System;

namespace Marius.Winter;

public struct Thickness
{
    public float Left, Top, Right, Bottom;

    public Thickness(float uniform) => Left = Top = Right = Bottom = uniform;
    public Thickness(float horizontal, float vertical)
    {
        Left = Right = horizontal;
        Top = Bottom = vertical;
    }
    public Thickness(float left, float top, float right, float bottom)
    {
        Left = left; Top = top; Right = right; Bottom = bottom;
    }

    public float HorizontalTotal => Left + Right;
    public float VerticalTotal => Top + Bottom;
}

public struct CornerRadius
{
    public float TopLeft, TopRight, BottomRight, BottomLeft;

    public CornerRadius(float uniform) => TopLeft = TopRight = BottomRight = BottomLeft = uniform;
    public CornerRadius(float topLeft, float topRight, float bottomRight, float bottomLeft)
    {
        TopLeft = topLeft; TopRight = topRight; BottomRight = bottomRight; BottomLeft = bottomLeft;
    }

    public bool IsUniform => TopLeft == TopRight && TopRight == BottomRight && BottomRight == BottomLeft;
    public bool IsZero => TopLeft == 0 && TopRight == 0 && BottomRight == 0 && BottomLeft == 0;
}

public struct RectF
{
    public float X, Y, W, H;

    public RectF(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }

    public float Right => X + W;
    public float Bottom => Y + H;
    public bool Contains(float px, float py) => px >= X && px < X + W && py >= Y && py < Y + H;

    public static RectF Empty => default;
}

public enum CursorType
{
    Arrow,
    IBeam,
    Hand,
    HResize,
    VResize,
    Crosshair,
    ResizeNWSE,
    ResizeNESW,
}

[Flags]
public enum ElementState
{
    Normal = 0,
    Hovered = 1,
    Pressed = 2,
    Focused = 4,
    Disabled = 8,
}

public class Style
{
    public Color4 Background;
    public Color4 BackgroundHovered;
    public Color4 BackgroundPressed;
    public Color4 BackgroundDisabled;
    public Color4 Foreground;
    public Color4 ForegroundDisabled;
    public Color4 Border;
    public Color4 BorderFocused;
    public float BorderWidth = 1f;
    public float CornerRadius = 3f;
    public float FontSize = 16f;
    public string FontName = "default";
    public Thickness Padding = new(8f, 4f);

    public Style Clone()
    {
        return (Style)MemberwiseClone();
    }
}

public struct Color4
{
    public float R, G, B, A;

    public Color4(float r, float g, float b, float a = 1f) { R = r; G = g; B = b; A = a; }
    /// <summary>Grayscale constructor matching nanogui's Color(intensity, alpha).</summary>
    public Color4(float intensity, float a) { R = G = B = intensity; A = a; }

    public byte R8 => (byte)(R * 255f + 0.5f);
    public byte G8 => (byte)(G * 255f + 0.5f);
    public byte B8 => (byte)(B * 255f + 0.5f);
    public byte A8 => (byte)(A * 255f + 0.5f);

    public static Color4 Lerp(Color4 a, Color4 b, float t)
    {
        return new Color4(
            a.R + (b.R - a.R) * t,
            a.G + (b.G - a.G) * t,
            a.B + (b.B - a.B) * t,
            a.A + (b.A - a.A) * t);
    }

    public static Color4 White => new(1f, 1f, 1f, 1f);
    public static Color4 Black => new(0f, 0f, 0f, 1f);
    public static Color4 Transparent => new(0f, 0f, 0f, 0f);
}

public class Theme
{
    public Style Button = new();
    public Style TextBox = new();
    public Style Panel = new();
    public Style Checkbox = new();
    public Style Slider = new();
    public Style Label = new();
    public Style ProgressBar = new();
    public Style Tooltip = new();
    public Style MenuBar = new();

    public Color4 WindowBackground;
    public Color4 DropShadow;
    public Color4 SelectionColor;
    public string DefaultFontName = "default";
    public float DefaultFontSize = 16f;

    // Nanogui-style border colors
    public Color4 BorderDark = new(29 / 255f, 1f);
    public Color4 BorderLight = new(92 / 255f, 1f);
    public Color4 BorderMedium = new(35 / 255f, 1f);

    // Nanogui-style text colors
    public Color4 TextColor = new(1f, 160 / 255f);
    public Color4 DisabledTextColor = new(1f, 80 / 255f);
    public Color4 TextColorShadow = new(0f, 160 / 255f);
    public Color4 CursorColor = new(1f, 192 / 255f, 0f, 1f);

    // Button gradient pairs (top -> bottom)
    public Color4 ButtonGradientTopUnfocused = new(74 / 255f, 1f);
    public Color4 ButtonGradientBotUnfocused = new(58 / 255f, 1f);
    public Color4 ButtonGradientTopFocused = new(64 / 255f, 1f);
    public Color4 ButtonGradientBotFocused = new(48 / 255f, 1f);
    public Color4 ButtonGradientTopPushed = new(41 / 255f, 1f);
    public Color4 ButtonGradientBotPushed = new(29 / 255f, 1f);

    public float ButtonCornerRadius = 2f;

    // --- Nanogui-inspired dark theme ---
    public static Theme Dark => new()
    {
        WindowBackground = new Color4(43 / 255f, 230 / 255f),
        DropShadow = new Color4(0f, 128 / 255f),
        SelectionColor = new Color4(1f, 1f, 1f, 80 / 255f),
        DefaultFontSize = 16f,

        // Nanogui border/text colors
        BorderDark = new Color4(29 / 255f, 1f),
        BorderLight = new Color4(92 / 255f, 1f),
        BorderMedium = new Color4(35 / 255f, 1f),
        TextColor = new Color4(1f, 160 / 255f),
        DisabledTextColor = new Color4(1f, 80 / 255f),
        TextColorShadow = new Color4(0f, 160 / 255f),
        CursorColor = new Color4(1f, 192 / 255f, 0f, 1f),

        // Button gradients
        ButtonGradientTopUnfocused = new Color4(74 / 255f, 1f),
        ButtonGradientBotUnfocused = new Color4(58 / 255f, 1f),
        ButtonGradientTopFocused = new Color4(64 / 255f, 1f),
        ButtonGradientBotFocused = new Color4(48 / 255f, 1f),
        ButtonGradientTopPushed = new Color4(41 / 255f, 1f),
        ButtonGradientBotPushed = new Color4(29 / 255f, 1f),
        ButtonCornerRadius = 2f,

        Button = new()
        {
            Background = new Color4(74 / 255f, 1f),
            BackgroundHovered = new Color4(64 / 255f, 1f),
            BackgroundPressed = new Color4(41 / 255f, 1f),
            BackgroundDisabled = new Color4(50 / 255f, 0.5f),
            Foreground = new Color4(1f, 160 / 255f),
            ForegroundDisabled = new Color4(1f, 80 / 255f),
            Border = new Color4(29 / 255f, 1f),
            BorderFocused = new Color4(0.25f, 0.50f, 0.85f, 1f),
            CornerRadius = 2f,
            Padding = new Thickness(16f, 6f),
            FontSize = 16f,
        },

        TextBox = new()
        {
            Background = new Color4(0f, 0f, 0f, 0.33f),
            BackgroundHovered = new Color4(0f, 0f, 0f, 0.40f),
            Foreground = new Color4(1f, 160 / 255f),
            ForegroundDisabled = new Color4(1f, 80 / 255f),
            Border = new Color4(0f, 0f, 0f, 48 / 255f),
            BorderFocused = new Color4(0.25f, 0.50f, 0.85f, 1f),
            CornerRadius = 3f,
            Padding = new Thickness(8f, 4f),
            FontSize = 16f,
        },

        Panel = new()
        {
            Background = new Color4(43 / 255f, 230 / 255f),
            Border = new Color4(29 / 255f, 1f),
            CornerRadius = 3f,
            BorderWidth = 0f,
        },

        Checkbox = new()
        {
            Background = new Color4(0f, 0f, 0f, 32 / 255f),
            BackgroundHovered = new Color4(0f, 0f, 0f, 66 / 255f),
            BackgroundPressed = new Color4(0f, 0f, 0f, 100 / 255f),
            Foreground = new Color4(1f, 160 / 255f),
            ForegroundDisabled = new Color4(1f, 80 / 255f),
            Border = new Color4(0f, 0f, 0f, 180 / 255f),
            BorderFocused = new Color4(0.25f, 0.50f, 0.85f, 1f),
            CornerRadius = 3f,
            FontSize = 16f,
        },

        Slider = new()
        {
            Background = new Color4(0f, 0f, 0f, 32 / 255f),
            Foreground = new Color4(150 / 255f, 1f),
            Border = new Color4(29 / 255f, 1f),
            BorderFocused = new Color4(0.25f, 0.50f, 0.85f, 1f),
            CornerRadius = 3f,
        },

        Label = new()
        {
            Background = Color4.Transparent,
            Foreground = new Color4(1f, 160 / 255f),
            ForegroundDisabled = new Color4(1f, 80 / 255f),
            BorderWidth = 0f,
            Padding = new Thickness(0f),
            FontSize = 16f,
        },

        ProgressBar = new()
        {
            Background = new Color4(0f, 0f, 0f, 92 / 255f),
            Foreground = new Color4(100 / 255f, 180 / 255f, 220 / 255f, 1f),
            Border = new Color4(29 / 255f, 1f),
            CornerRadius = 3f,
        },

        Tooltip = new()
        {
            Background = new Color4(50 / 255f, 50 / 255f, 50 / 255f, 240 / 255f),
            Foreground = new Color4(1f, 1f, 1f, 0.9f),
            Border = new Color4(80 / 255f, 80 / 255f, 80 / 255f, 1f),
            CornerRadius = 4f,
            BorderWidth = 1f,
            Padding = new Thickness(8f, 4f),
            FontSize = 13f,
        },

        MenuBar = new()
        {
            Background = new Color4(35 / 255f, 35 / 255f, 35 / 255f, 1f),
            Foreground = new Color4(1f, 160 / 255f),
            Border = new Color4(29 / 255f, 1f),
            FontSize = 14f,
            Padding = new Thickness(10f, 3f),
        },
    };

    // --- Light theme ---
    public static Theme Light => new()
    {
        WindowBackground = new Color4(0.94f, 0.94f, 0.94f, 1f),
        DropShadow = new Color4(0f, 0f, 0f, 0.15f),
        SelectionColor = new Color4(0.25f, 0.50f, 0.85f, 0.35f),
        DefaultFontSize = 16f,

        BorderDark = new Color4(0.60f, 1f),
        BorderLight = new Color4(0.92f, 1f),
        BorderMedium = new Color4(0.70f, 1f),
        TextColor = new Color4(0.10f, 0.10f, 0.10f, 1f),
        DisabledTextColor = new Color4(0.50f, 0.50f, 0.50f, 1f),
        TextColorShadow = new Color4(1f, 1f, 1f, 0.3f),
        CursorColor = new Color4(0f, 0.45f, 0.85f, 1f),

        ButtonGradientTopUnfocused = new Color4(0.92f, 1f),
        ButtonGradientBotUnfocused = new Color4(0.85f, 1f),
        ButtonGradientTopFocused = new Color4(0.88f, 1f),
        ButtonGradientBotFocused = new Color4(0.80f, 1f),
        ButtonGradientTopPushed = new Color4(0.78f, 1f),
        ButtonGradientBotPushed = new Color4(0.70f, 1f),
        ButtonCornerRadius = 2f,

        Button = new()
        {
            Background = new Color4(0.88f, 0.88f, 0.88f, 1f),
            BackgroundHovered = new Color4(0.93f, 0.93f, 0.93f, 1f),
            BackgroundPressed = new Color4(0.78f, 0.78f, 0.78f, 1f),
            BackgroundDisabled = new Color4(0.85f, 0.85f, 0.85f, 0.5f),
            Foreground = new Color4(0.10f, 0.10f, 0.10f, 1f),
            ForegroundDisabled = new Color4(0.50f, 0.50f, 0.50f, 1f),
            Border = new Color4(0.60f, 0.60f, 0.60f, 1f),
            BorderFocused = new Color4(0.25f, 0.50f, 0.85f, 1f),
            CornerRadius = 2f,
            Padding = new Thickness(16f, 6f),
            FontSize = 16f,
        },

        TextBox = new()
        {
            Background = new Color4(1f, 1f, 1f, 1f),
            BackgroundHovered = new Color4(1f, 1f, 1f, 1f),
            Foreground = new Color4(0.10f, 0.10f, 0.10f, 1f),
            ForegroundDisabled = new Color4(0.50f, 0.50f, 0.50f, 1f),
            Border = new Color4(0f, 0f, 0f, 48 / 255f),
            BorderFocused = new Color4(0.25f, 0.50f, 0.85f, 1f),
            CornerRadius = 3f,
            Padding = new Thickness(8f, 4f),
            FontSize = 16f,
        },

        Panel = new()
        {
            Background = new Color4(0.94f, 0.94f, 0.94f, 1f),
            Border = new Color4(0.78f, 0.78f, 0.78f, 1f),
            CornerRadius = 3f,
            BorderWidth = 0f,
        },

        Checkbox = new()
        {
            Background = new Color4(1f, 1f, 1f, 1f),
            BackgroundHovered = new Color4(0.96f, 0.96f, 0.98f, 1f),
            Foreground = new Color4(0.10f, 0.10f, 0.10f, 1f),
            ForegroundDisabled = new Color4(0.50f, 0.50f, 0.50f, 1f),
            Border = new Color4(0.65f, 0.65f, 0.65f, 1f),
            BorderFocused = new Color4(0.25f, 0.50f, 0.85f, 1f),
            CornerRadius = 3f,
            FontSize = 16f,
        },

        Slider = new()
        {
            Background = new Color4(0.78f, 0.78f, 0.80f, 1f),
            Foreground = new Color4(0.55f, 0.55f, 0.55f, 1f),
            Border = new Color4(0.65f, 0.65f, 0.65f, 1f),
            BorderFocused = new Color4(0.25f, 0.50f, 0.85f, 1f),
            CornerRadius = 3f,
        },

        Label = new()
        {
            Background = Color4.Transparent,
            Foreground = new Color4(0.10f, 0.10f, 0.10f, 1f),
            ForegroundDisabled = new Color4(0.50f, 0.50f, 0.50f, 1f),
            BorderWidth = 0f,
            Padding = new Thickness(0f),
            FontSize = 16f,
        },

        ProgressBar = new()
        {
            Background = new Color4(0.85f, 0.85f, 0.87f, 1f),
            Foreground = new Color4(0.25f, 0.50f, 0.85f, 1f),
            Border = new Color4(0.65f, 0.65f, 0.65f, 1f),
            CornerRadius = 3f,
        },

        Tooltip = new()
        {
            Background = new Color4(1f, 1f, 1f, 0.95f),
            Foreground = new Color4(0.1f, 0.1f, 0.1f, 1f),
            Border = new Color4(0.70f, 0.70f, 0.70f, 1f),
            CornerRadius = 4f,
            BorderWidth = 1f,
            Padding = new Thickness(8f, 4f),
            FontSize = 13f,
        },

        MenuBar = new()
        {
            Background = new Color4(0.92f, 0.92f, 0.92f, 1f),
            Foreground = new Color4(0.10f, 0.10f, 0.10f, 1f),
            Border = new Color4(0.70f, 0.70f, 0.70f, 1f),
            FontSize = 14f,
            Padding = new Thickness(10f, 3f),
        },
    };
}
