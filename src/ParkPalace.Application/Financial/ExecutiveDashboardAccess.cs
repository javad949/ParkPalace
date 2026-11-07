using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Constants;

namespace ParkPalace.Application.Financial;

/// <summary>
/// Access rules for the executive decision dashboard.
/// Ops KPIs use Reports.* / operational roles.
/// Sepidar money figures require Finance.PartyLedgerView exclusively (Admin alone is not enough).
/// </summary>
public static class ExecutiveDashboardAccess
{
    public static bool CanViewOps(ICurrentUserService user)
    {
        if (!user.IsAuthenticated) return false;
        if (user.Roles.Any(r =>
                string.Equals(r, Roles.Manager, StringComparison.OrdinalIgnoreCase)
                || string.Equals(r, Roles.Finance, StringComparison.OrdinalIgnoreCase)
                || string.Equals(r, Roles.Procurement, StringComparison.OrdinalIgnoreCase)
                || string.Equals(r, Roles.Warehouse, StringComparison.OrdinalIgnoreCase)
                || string.Equals(r, Roles.OrganizationManager, StringComparison.OrdinalIgnoreCase)
                || string.Equals(r, Roles.Admin, StringComparison.OrdinalIgnoreCase)))
            return true;

        return user.HasPermission(Permissions.ReportsView)
            || user.HasPermission(Permissions.ReportsManager)
            || user.HasPermission(Permissions.ReportsFinance)
            || user.HasPermission(Permissions.ReportsProcurement)
            || user.HasPermission(Permissions.ReportsWarehouse)
            || user.HasPermission(Permissions.ReportsAdmin)
            || user.HasPermission(Permissions.OrgReportsView);
    }

    public static bool CanViewFinanceFigures(ICurrentUserService user) =>
        FinancialAccess.CanViewPartyLedger(user);
}
