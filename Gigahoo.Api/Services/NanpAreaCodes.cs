namespace Gigahoo.Api.Services;

/// <summary>
/// NANP (North American Numbering Plan) area-code helpers. US and Canada both
/// share the +1 country code, so the area code (first 3 digits after +1) is the
/// only thing that disambiguates them. This lets us reject e.g. a US-area-code
/// number signing up as Canada (and vice versa).
/// </summary>
public static class NanpAreaCodes
{
    // Canadian NANP area codes (under +1). Any +1 area code NOT in this set is
    // treated as US.
    private static readonly HashSet<string> Canadian = new(StringComparer.Ordinal)
    {
        "204", "226", "236", "249", "250", "263", "289", "306", "343", "354", "365",
        "367", "368", "382", "387", "403", "416", "418", "431", "437", "438", "450",
        "468", "474", "506", "514", "519", "548", "579", "581", "584", "587", "604",
        "613", "639", "647", "672", "683", "705", "709", "742", "753", "778", "780",
        "782", "807", "819", "825", "867", "873", "879", "902", "905",
    };

    /// <summary>
    /// Validate that a +1 (NANP) phone's area code matches the selected country.
    /// Only US/CA are ambiguous (both +1); every other country has a distinct
    /// dial code, so this returns true (no constraint) for them. For US the area
    /// code must NOT be Canadian; for CA it must be Canadian. Returns true when
    /// the number is too short to have a full 3-digit area code (length
    /// validation handles that case).
    /// </summary>
    public static bool MatchesCountry(string? phone, string? countryCode)
    {
        if (countryCode is not ("US" or "CA")) return true;

        var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
        // Drop a leading "1" country-code digit if present (11-digit NANP form).
        if (digits.Length == 11 && digits[0] == '1') digits = digits[1..];
        if (digits.Length < 3) return true;

        var areaCode = digits[..3];
        var isCanadian = Canadian.Contains(areaCode);
        return countryCode == "CA" ? isCanadian : !isCanadian;
    }
}
