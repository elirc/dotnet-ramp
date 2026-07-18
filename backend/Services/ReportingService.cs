using ExpenseManagement.Api.Domain;
using ExpenseManagement.Api.Dtos;

namespace ExpenseManagement.Api.Services;

/// <summary>
/// Aggregated spend reporting, scoped to what the caller is allowed to see
/// (reusing <see cref="ExpenseService.List"/> so visibility rules live in one place).
/// </summary>
public class ReportingService
{
    private readonly ExpenseService _expenses;

    public ReportingService(ExpenseService expenses)
    {
        _expenses = expenses;
    }

    public ReportSummaryResponse Summarize(User actor, DateOnly? from, DateOnly? to)
    {
        var visible = _expenses.List(actor, new ExpenseFilter(null, null, from, to));

        decimal TotalIn(params ExpenseStatus[] statuses) =>
            visible.Where(e => statuses.Contains(e.Status)).Sum(e => e.Amount);

        var byCategory = visible
            .GroupBy(e => e.Category)
            .Select(g => new CategoryTotal(g.Key.ToString(), g.Sum(e => e.Amount), g.Count()))
            .OrderByDescending(c => c.Total)
            .ToList();

        var byStatus = visible
            .GroupBy(e => e.Status)
            .Select(g => new StatusTotal(g.Key.ToString(), g.Sum(e => e.Amount), g.Count()))
            .OrderBy(s => s.Status)
            .ToList();

        int pendingApproval = visible.Count(e => e.Status == ExpenseStatus.Submitted);

        return new ReportSummaryResponse(
            TotalSubmitted: TotalIn(ExpenseStatus.Submitted),
            TotalApproved: TotalIn(ExpenseStatus.Approved, ExpenseStatus.Reimbursing),
            TotalReimbursed: TotalIn(ExpenseStatus.Reimbursed),
            PendingApprovalCount: pendingApproval,
            ByCategory: byCategory,
            ByStatus: byStatus);
    }
}
