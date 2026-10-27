using System.Text.RegularExpressions;

namespace SatPlatform.Services;

/// <summary>Uzbekistan phone numbers: stored as "+998901234567", shown as "+998 90 123 45 67".
/// The input mask in wwwroot/app.js (data-phone) mirrors these rules on the client; keep them in sync.</summary>
public static partial class PhoneFormat
{
    public const string InvalidMessage = "Telefon raqam noto'g'ri. O'zbekiston raqamini kiriting, masalan: +998 90 123 45 67";

    // Mobile operator codes of Uzbekistan (Telegram needs a mobile number).
    [GeneratedRegex(@"^\+998(20|33|50|55|77|88|90|91|93|94|95|97|98|99)\d{7}$")]
    private static partial Regex ValidRegex();

    private static string Digits(string? s) => new((s ?? "").Where(char.IsDigit).ToArray());

    /// <summary>"90 123 45 67", "0901234567", "998901234567", "+998 (90) 123-45-67" → "+998901234567". Anything else → "+" + its digits (which fails <see cref="IsValid"/>).</summary>
    public static string Normalize(string? input)
    {
        var digits = Digits(input);
        if (digits.Length == 9) digits = "998" + digits;
        else if (digits.Length == 10 && digits[0] == '0') digits = "998" + digits[1..];
        return "+" + digits;
    }

    public static bool IsValid(string? input) => ValidRegex().IsMatch(Normalize(input));

    /// <summary>Search box match: "90 123", "+998 90" or "1234567" all find "+998901234567"; text without digits matches nothing.</summary>
    public static bool Matches(string? phone, string? search)
    {
        var digits = Digits(search);
        return digits.Length > 0 && (phone ?? "").Contains(digits);
    }

    /// <summary>"+998901234567" → "+998 90 123 45 67". Unrecognised input is returned unchanged.</summary>
    public static string Display(string? phone)
    {
        var n = Normalize(phone);
        return ValidRegex().IsMatch(n) ? $"+998 {n[4..6]} {n[6..9]} {n[9..11]} {n[11..13]}" : phone ?? "";
    }
}
