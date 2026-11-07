namespace ParkPalace.Application.Financial;

/// <summary>
/// Picks trade receivables and payables from subsidiary-account titles in this company's chart.
/// Codes from another company's warehouse are never used.
/// </summary>
public static class TradeAccountClassifier
{
    public const string Receivable = "Receivable";
    public const string Payable = "Payable";

    public const string Definition =
        "رقم، جمع ردیف‌های ACC.VoucherItem است روی معین‌هایی که عنوان خودِ این شرکت دریافتنی یا پرداختنی است. مالیات، حقوق، بیمه، کارکنان، وام، سپرده، ودیعه و حساب انتظامی کنار گذاشته شده‌اند. وضعیت سند فیلتر نشده است. مطالبات بدهکار منهای بستانکار است و بدهی‌ها بستانکار منهای بدهکار.";

    public static string? Side(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var text = Normalize(title);
        var receivable = text.Contains("دریافتنی", StringComparison.Ordinal);
        var payable = text.Contains("پرداختنی", StringComparison.Ordinal);
        if (receivable == payable)
            return null;
        if (IsExcluded(text))
            return null;
        return receivable ? Receivable : Payable;
    }

    public static string Normalize(string title)
    {
        return title.Trim()
            .Replace("\u200c", string.Empty)
            .Replace("\u200d", string.Empty)
            .Replace('ي', 'ی')
            .Replace('ك', 'ک')
            .Replace('ة', 'ه');
    }

    private static bool IsExcluded(string text)
    {
        string[] excluded =
        {
            "مالیات", "عوارض", "حقوق", "دستمزد", "بیمه", "کارکن", "پرسنل",
            "وام", "مساعده", "پاداش", "علی الحساب", "سپرده", "ودیعه", "انتظامی", "سود سهام"
        };
        foreach (var word in excluded)
        {
            if (text.Contains(word, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
