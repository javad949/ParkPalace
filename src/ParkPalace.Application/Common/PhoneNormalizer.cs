using System.Text.RegularExpressions;

namespace ParkPalace.Application.Common;

public static class PhoneNormalizer
{
    private static readonly string[] PersianDigits = { "۰", "۱", "۲", "۳", "۴", "۵", "۶", "۷", "۸", "۹" };
    private static readonly string[] ArabicDigits = { "٠", "١", "٢", "٣", "٤", "٥", "٦", "٧", "٨", "٩" };

    public static string Normalize(string? rawPhone)
    {
        if (string.IsNullOrWhiteSpace(rawPhone))
            return string.Empty;

        // 1. Convert Persian/Arabic digits to ASCII
        var normalized = rawPhone.Trim();
        for (int i = 0; i < 10; i++)
        {
            normalized = normalized.Replace(PersianDigits[i], i.ToString());
            normalized = normalized.Replace(ArabicDigits[i], i.ToString());
        }

        // 2. Remove all non-digit characters
        normalized = Regex.Replace(normalized, @"[^\d]", "");

        // 3. Normalize international prefixes
        // 0098 -> 0
        if (normalized.StartsWith("0098"))
        {
            normalized = "0" + normalized.Substring(4);
        }
        // 98 -> 0 (if followed by 9 for mobile or 2-digit area code for landline)
        else if (normalized.StartsWith("98") && normalized.Length >= 11)
        {
            normalized = "0" + normalized.Substring(2);
        }

        // 4. Mobile numbers: if starts with 9 and length 10 -> prepend 0
        if (normalized.Length == 10 && normalized.StartsWith("9"))
        {
            normalized = "0" + normalized;
        }

        return normalized;
    }

    public static bool IsValidIranianMobile(string normalizedPhone)
    {
        if (string.IsNullOrWhiteSpace(normalizedPhone))
            return false;

        return Regex.IsMatch(normalizedPhone, @"^09\d{9}$");
    }

    public static bool IsValidIranianLandline(string normalizedPhone)
    {
        if (string.IsNullOrWhiteSpace(normalizedPhone))
            return false;

        return Regex.IsMatch(normalizedPhone, @"^0\d{10}$");
    }
}
