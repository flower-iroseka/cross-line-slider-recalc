using System.Globalization;

namespace CrossLineSliderRecalc;

/// <summary>
///     Reads the list of beat spacings the custom alignment option uses.
/// </summary>
/// <remarks>
///     An item is how far apart the wanted grid lines are, counted in beats, so <c>1/2</c> is the
///     half-beat grid, <c>1/12</c> is a twelfth of a beat, and <c>2</c> is every second white line.
/// </remarks>
public static class BeatSpacingParser
{
    /// <summary>
    ///     Parses a comma-separated list of beat spacings into an ascending list without duplicates.
    /// </summary>
    /// <param name="text">
    ///     The raw text. Each item is a whole number, a decimal of any length, or a fraction, so
    ///     <c>1/12, 0.75, 2</c> asks for a twelfth of a beat, three quarters of a beat and two beats.
    /// </param>
    /// <param name="spacings">The parsed spacings in ascending order.</param>
    /// <returns><see langword="true" /> when at least one usable spacing was found and every item
    /// that was not blank parsed.</returns>
    public static bool TryParse(string? text, out IReadOnlyList<double> spacings)
    {
        spacings = [];
        if (string.IsNullOrWhiteSpace(text)) return false;

        SortedSet<double> parsed = [];
        foreach (string item in text.Split(','))
        {
            string trimmed = item.Trim();

            // A trailing or doubled comma is a slip while typing, not a wrong list.
            if (trimmed.Length == 0) continue;

            if (!TryParseItem(trimmed, out double spacing)) return false;

            parsed.Add(spacing);
        }

        if (parsed.Count == 0) return false;

        spacings = [.. parsed];
        return true;
    }

    /// <summary>Parses one item, which is either a number or a fraction of two numbers.</summary>
    private static bool TryParseItem(string item, out double spacing)
    {
        spacing = 0;

        int slash = item.IndexOf('/');
        if (slash < 0)
        {
            if (!TryParseNumber(item, out spacing)) return false;
        }
        else
        {
            // Exactly one slash, between two numbers. A zero below the line divides to infinity,
            // which the test at the end rejects along with everything else that is not a distance.
            if (item.IndexOf('/', slash + 1) >= 0) return false;

            if (!TryParseNumber(item[..slash].Trim(), out double numerator)) return false;
            if (!TryParseNumber(item[(slash + 1)..].Trim(), out double denominator)) return false;

            spacing = numerator / denominator;
        }

        return double.IsFinite(spacing) && spacing > 0;
    }

    /// <summary>
    ///     Parses a plain or fractional number. Exponent notation is left out on purpose, so that a
    ///     slip like <c>1e3</c> is reported instead of being read as 1000 beats.
    /// </summary>
    private static bool TryParseNumber(string text, out double value)
    {
        const NumberStyles style = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign;

        return double.TryParse(text, style, CultureInfo.InvariantCulture, out value)
               && double.IsFinite(value);
    }
}
