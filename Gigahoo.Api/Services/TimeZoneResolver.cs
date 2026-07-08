namespace Gigahoo.Api.Services;

/// <summary>
/// Maps an account's region (US state / CA province / MX state) to an IANA timezone so call times
/// can be shown in the account owner's LOCAL time in emails (which, unlike the dashboard, have no
/// browser timezone to rely on). Falls back to UTC for anything unmapped.
/// </summary>
public static class TimeZoneResolver
{
    private static readonly Dictionary<string, string> RegionToIana = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Canada ──
        ["British Columbia"] = "America/Vancouver",
        ["Alberta"] = "America/Edmonton",
        ["Saskatchewan"] = "America/Regina",
        ["Manitoba"] = "America/Winnipeg",
        ["Ontario"] = "America/Toronto",
        ["Quebec"] = "America/Toronto",
        ["New Brunswick"] = "America/Moncton",
        ["Nova Scotia"] = "America/Halifax",
        ["Prince Edward Island"] = "America/Halifax",
        ["Newfoundland and Labrador"] = "America/St_Johns",
        ["Yukon"] = "America/Whitehorse",
        ["Northwest Territories"] = "America/Yellowknife",
        ["Nunavut"] = "America/Iqaluit",
        // ── United States ──
        ["Alabama"] = "America/Chicago",
        ["Alaska"] = "America/Anchorage",
        ["Arizona"] = "America/Phoenix",
        ["Arkansas"] = "America/Chicago",
        ["California"] = "America/Los_Angeles",
        ["Colorado"] = "America/Denver",
        ["Connecticut"] = "America/New_York",
        ["Delaware"] = "America/New_York",
        ["Florida"] = "America/New_York",
        ["Georgia"] = "America/New_York",
        ["Hawaii"] = "Pacific/Honolulu",
        ["Idaho"] = "America/Boise",
        ["Illinois"] = "America/Chicago",
        ["Indiana"] = "America/Indiana/Indianapolis",
        ["Iowa"] = "America/Chicago",
        ["Kansas"] = "America/Chicago",
        ["Kentucky"] = "America/New_York",
        ["Louisiana"] = "America/Chicago",
        ["Maine"] = "America/New_York",
        ["Maryland"] = "America/New_York",
        ["Massachusetts"] = "America/New_York",
        ["Michigan"] = "America/Detroit",
        ["Minnesota"] = "America/Chicago",
        ["Mississippi"] = "America/Chicago",
        ["Missouri"] = "America/Chicago",
        ["Montana"] = "America/Denver",
        ["Nebraska"] = "America/Chicago",
        ["Nevada"] = "America/Los_Angeles",
        ["New Hampshire"] = "America/New_York",
        ["New Jersey"] = "America/New_York",
        ["New Mexico"] = "America/Denver",
        ["New York"] = "America/New_York",
        ["North Carolina"] = "America/New_York",
        ["North Dakota"] = "America/Chicago",
        ["Ohio"] = "America/New_York",
        ["Oklahoma"] = "America/Chicago",
        ["Oregon"] = "America/Los_Angeles",
        ["Pennsylvania"] = "America/New_York",
        ["Rhode Island"] = "America/New_York",
        ["South Carolina"] = "America/New_York",
        ["South Dakota"] = "America/Chicago",
        ["Tennessee"] = "America/Chicago",
        ["Texas"] = "America/Chicago",
        ["Utah"] = "America/Denver",
        ["Vermont"] = "America/New_York",
        ["Virginia"] = "America/New_York",
        ["Washington"] = "America/Los_Angeles",
        ["West Virginia"] = "America/New_York",
        ["Wisconsin"] = "America/Chicago",
        ["Wyoming"] = "America/Denver",
        ["District of Columbia"] = "America/New_York",
        // ── Mexico ──
        ["Baja California"] = "America/Tijuana",
        ["Baja California Sur"] = "America/Mazatlan",
        ["Sonora"] = "America/Hermosillo",
        ["Chihuahua"] = "America/Chihuahua",
        ["Sinaloa"] = "America/Mazatlan",
        ["Nayarit"] = "America/Mazatlan",
        ["Quintana Roo"] = "America/Cancun",
        // remaining MX states are Central time
        ["Aguascalientes"] = "America/Mexico_City",
        ["Campeche"] = "America/Mexico_City",
        ["Chiapas"] = "America/Mexico_City",
        ["Coahuila"] = "America/Mexico_City",
        ["Colima"] = "America/Mexico_City",
        ["Durango"] = "America/Mexico_City",
        ["Guanajuato"] = "America/Mexico_City",
        ["Guerrero"] = "America/Mexico_City",
        ["Hidalgo"] = "America/Mexico_City",
        ["Jalisco"] = "America/Mexico_City",
        ["México"] = "America/Mexico_City",
        ["Mexico City"] = "America/Mexico_City",
        ["Michoacán"] = "America/Mexico_City",
        ["Morelos"] = "America/Mexico_City",
        ["Nuevo León"] = "America/Monterrey",
        ["Oaxaca"] = "America/Mexico_City",
        ["Puebla"] = "America/Mexico_City",
        ["Querétaro"] = "America/Mexico_City",
        ["San Luis Potosí"] = "America/Mexico_City",
        ["Tabasco"] = "America/Mexico_City",
        ["Tamaulipas"] = "America/Matamoros",
        ["Tlaxcala"] = "America/Mexico_City",
        ["Veracruz"] = "America/Mexico_City",
        ["Yucatán"] = "America/Merida",
        ["Zacatecas"] = "America/Mexico_City",
    };

    /// <summary>Format a UTC time in the account region's local timezone, e.g. "Jul 8, 2026, 11:53 AM (UTC-07:00)".</summary>
    public static string FormatLocal(DateTime utc, string? region)
    {
        var utcTime = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        if (region is not null && RegionToIana.TryGetValue(region, out var iana))
        {
            try
            {
                var info = TimeZoneInfo.FindSystemTimeZoneById(iana);
                var local = TimeZoneInfo.ConvertTimeFromUtc(utcTime, info);
                var off = info.GetUtcOffset(local);
                var offStr = $"UTC{(off < TimeSpan.Zero ? "-" : "+")}{Math.Abs(off.Hours):D2}:{Math.Abs(off.Minutes):D2}";
                return $"{local:MMM d, yyyy, h:mm tt} ({offStr})";
            }
            catch
            {
                // timezone db not found on host — fall through to UTC
            }
        }
        return $"{utcTime:MMM d, yyyy, h:mm tt} (UTC)";
    }
}
