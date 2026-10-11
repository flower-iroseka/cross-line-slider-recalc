using System.Globalization;

namespace CrossLineSliderRecalc;

/// <summary>
///     Reads the target time a user typed into the custom-time box.
/// </summary>
public static class TargetTimeParser
{
    /// <summary>
    ///     Parses either a plain millisecond count such as <c>16571</c> or an osu! editor
    ///     timestamp such as <c>00:16:571</c>.
    /// </summary>
    /// <param name="text">
    ///     The raw text, which may have whitespace around it. Everything after the first
    ///     whitespace-separated field is ignored, because a time copied out of the editor can carry
    ///     what followed it on the same line, such as <c>00:16:571 - 150 BPM</c>.
    /// </param>
    /// <param name="milliseconds">The parsed time in milliseconds.</param>
    /// <returns><see langword="true" /> when the text starts with a time in one of the two forms.</returns>
    public static bool TryParse(string? text, out double milliseconds)
    {
        milliseconds = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string trimmed = FirstField(text.Trim());

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double plain))
        {
            if (!double.IsFinite(plain)) return false;
            milliseconds = plain;
            return true;
        }

        // The editor copies timestamps as mm:ss:mmm, with the third field always three digits. The
        // three digits cap that field at 999 on their own, so only the seconds need a range check.
        string[] fields = trimmed.Split(':');
        if (fields.Length != 3) return false;
        if (!TryParseNumber(fields[0], out int minutes)) return false;
        if (!TryParseNumber(fields[1], out int seconds) || seconds > 59) return false;
        if (fields[2].Length != 3) return false;
        if (!TryParseNumber(fields[2], out int thousandths)) return false;

        milliseconds = ((minutes * 60L) + seconds) * 1000L + thousandths;
        return true;
    }

    /// <summary>Everything up to the first whitespace, which may be the whole text.</summary>
    private static string FirstField(string trimmed)
    {
        int end = 0;
        while (end < trimmed.Length && !char.IsWhiteSpace(trimmed[end])) end++;
        return trimmed[..end];
    }

    private static bool TryParseNumber(string text, out int value)
    {
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
}
