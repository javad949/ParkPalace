using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Constants;

namespace ParkPalace.Application.Financial;

/// <summary>
/// Party balance + document ledger require Finance.PartyLedgerView exclusively.
/// Finance.View, Finance.Reconcile, and System Admin alone do not open the ledger.
/// Reconciliation endpoints keep using Finance.Reconcile separately.
/// </summary>
public static class FinancialAccess
{
    public static bool CanViewPartyLedger(ICurrentUserService user)
    {
        if (!user.IsAuthenticated) return false;
        return user.HasPermission(Permissions.FinancePartyLedgerView);
    }

    /// <summary>Alias for party balance / ledger totals.</summary>
    public static bool CanViewTotals(ICurrentUserService user) => CanViewPartyLedger(user);

    /// <summary>Document lines of the Sepidar party ledger.</summary>
    public static bool CanViewDocumentLines(ICurrentUserService user) => CanViewPartyLedger(user);

    /// <summary>Invoices / matching / reconcile dashboards — not the Sepidar party ledger.</summary>
    public static bool CanViewFinanceOverview(ICurrentUserService user)
    {
        if (!user.IsAuthenticated) return false;
        return user.HasPermission(Permissions.FinanceView)
            || user.HasPermission(Permissions.FinancePartyLedgerView)
            || user.HasPermission(Permissions.FinanceReconcile)
            || user.HasPermission(Permissions.InvoicesView)
            || user.HasPermission(Permissions.MatchingView);
    }

    public static bool CanReconcile(ICurrentUserService user)
    {
        if (!user.IsAuthenticated) return false;
        return user.HasPermission(Permissions.FinanceReconcile);
    }
}
