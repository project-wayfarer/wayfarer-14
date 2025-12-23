namespace Content.Shared._Coyote;
public static class ColorExtensions
{
    /// <summary>
    /// takes a string input and returns a color based on a consistent random seed using the input as a seed.
    /// when the same input is given, the same color will be returned.
    /// </summary>
    /// <param name="input"></param>
    /// <returns>A color</returns>
    public static Color ConsistentRandomSeededColorFromString(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            input = "bingles"; // Return transparent color for empty or null input
        }
        // Use a consistent hash function to generate a seed from the input string
        int seed = input.GetHashCode();
        System.Random random = new(seed);

        // Generate random RGB values
        byte r = (byte)random.Next(0, 256);
        byte g = (byte)random.Next(0, 256);
        byte b = (byte)random.Next(0, 256);

        return new Color(r, g, b, 255); // A is set to 255 (opaque)
    }

    /// <summary>
    /// The backbround is 44, 44, 46, 255
    /// If a color is darker than the background color, or just barely brighter than it, it will be adjusted, violently
    /// Color's vars are between 0 and 1, and must be clamped to that range.
    /// This can't go wrong in any way
    /// </summary>
    public static Color PreventColorFromBeingTooCloseToTheBackgroundColor(Color theColor)
    {
        Color backgroundColor = new(44, 44, 46, 255);
        // Calculate the brightness of the color
        var brightness = (theColor.R * 0.299 + theColor.G * 0.587 + theColor.B * 0.114) / 255;
        // Calculate the brightness of the background color
        var backgroundBrightness = (backgroundColor.R * 0.299 + backgroundColor.G * 0.587 + backgroundColor.B * 0.114) / 255;
        // If the color is too close to the background color, adjust it
        if (!(brightness < backgroundBrightness + 0.1) && !(brightness > backgroundBrightness - 0.1))
            return theColor;
        // R, G, and B are floats between 0 and 1
        // Adjust the color by increasing its brightness
        theColor.R = Math.Clamp(theColor.R + 0.2f, 0f, 1f);
        theColor.G = Math.Clamp(theColor.G + 0.2f, 0f, 1f);
        theColor.B = Math.Clamp(theColor.B + 0.2f, 0f, 1f);
        theColor.A = Math.Clamp(theColor.A, 0f, 1f);
        // If the color is not too close to the background color, return it unchanged
        return theColor;
    }

}
