using System.Drawing;

namespace AutoTestClient.DataProcessing;

/// <summary>
/// C# implementation of the reference analyzer's colour formula.
///
/// The reference builds RGB values from a normalized value using three
/// piecewise channels (3*scale), then converts each channel to an 8-bit
/// integer by truncation.  Keeping this in one place makes the embedded
/// preview and exported PNG use exactly the same colours without invoking
/// the reference Python application.
/// </summary>
public static class CrosstalkColorMap
{
    /// <summary>Map a normalized value in [0, 1] to the reference RGB colour.</summary>
    public static Color Jet(double normalized)
    {
        // The reference paints non-finite samples as a neutral gray before
        // applying the user exclusion overlay.  Keep the public helper safe
        // for callers that pass a raw NaN/Infinity value as well.
        if (!double.IsFinite(normalized)) return Color.FromArgb(90, 90, 90);
        double value = Math.Clamp(normalized, 0d, 1d);
        double red = Math.Clamp(3d * value - 1d, 0d, 1d);
        double green = Math.Clamp(1.5d - Math.Abs(3d * value - 1.5d), 0d, 1d);
        double blue = Math.Clamp(2d - 3d * value, 0d, 1d);
        return Color.FromArgb(ToByte(red), ToByte(green), ToByte(blue));
    }

    private static int ToByte(double channel) =>
        Math.Clamp((int)(channel * 255d), 0, 255);
}
