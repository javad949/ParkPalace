namespace ParkPalace.Application.Financial;

/// <summary>
/// Compares a fiscal year with the same elapsed span of the previous fiscal year.
/// The previous year is supplied by the caller. This type never invents a year by adding
/// twelve months, and it never sums daily balances.
/// </summary>
public static class FiscalPeriodComparison
{
    public static FiscalComparisonWindow CompareElapsed(
        DateTime currentStart,
        DateTime currentEndExclusive,
        DateTime previousStart,
        DateTime previousEndExclusive,
        DateTime asOf)
    {
        if (currentEndExclusive <= currentStart)
            throw new ArgumentException("پایان سال مالی جاری باید بعد از شروع آن باشد.");
        if (previousEndExclusive <= previousStart)
            throw new ArgumentException("پایان سال مالی قبل باید بعد از شروع آن باشد.");

        var clampedAsOf = asOf < currentStart ? currentStart : asOf;
        if (clampedAsOf > currentEndExclusive)
            clampedAsOf = currentEndExclusive;

        var elapsed = clampedAsOf - currentStart;
        var previousElapsedEnd = previousStart + elapsed;
        if (previousElapsedEnd > previousEndExclusive)
            previousElapsedEnd = previousEndExclusive;

        return new FiscalComparisonWindow(
            currentStart,
            clampedAsOf,
            previousStart,
            previousElapsedEnd,
            elapsed);
    }
}

public readonly record struct FiscalComparisonWindow(
    DateTime CurrentFrom,
    DateTime CurrentTo,
    DateTime PreviousFrom,
    DateTime PreviousTo,
    TimeSpan Elapsed);
