using ExpenseManagement.Api.Domain;
using ExpenseManagement.Api.Dtos;
using ExpenseManagement.Api.Storage;

namespace ExpenseManagement.Api.Services;

/// <summary>Maps domain entities to response DTOs (never leaks password hashes, file paths, etc.).</summary>
public static class Mappings
{
    public static UserResponse ToResponse(this User u) =>
        new(u.Id, u.Email, u.FullName, u.Role.ToString(), u.ManagerId);

    public static ReceiptResponse ToResponse(this Receipt r) =>
        new(r.Id, r.OriginalFileName, r.ContentType, r.SizeBytes,
            r.Status.ToString(), r.ExtractedAmount, r.UploadedAt);

    public static ExpenseResponse ToResponse(this Expense e, DataStore store, User actor)
    {
        var employee = store.Users.GetValueOrDefault(e.EmployeeId);
        var receipt = e.ReceiptId is { } rid ? store.Receipts.GetValueOrDefault(rid) : null;

        return new ExpenseResponse(
            e.Id,
            e.EmployeeId,
            employee?.FullName ?? "(unknown)",
            e.Merchant,
            e.Description,
            e.Amount,
            e.Currency,
            e.Category.ToString(),
            e.ExpenseDate,
            e.Status.ToString(),
            e.RejectionReason,
            e.PaymentReference,
            receipt?.ToResponse(),
            e.PolicyFlags.Select(f => new PolicyFlagResponse(f.Code, f.Message, f.Severity.ToString())).ToList(),
            e.History.Select(h => new ExpenseEventResponse(
                h.From.ToString(), h.To.ToString(), h.ActorName, h.Note, h.At)).ToList(),
            AvailableActions(e, actor, store),
            e.CreatedAt,
            e.UpdatedAt);
    }

    /// <summary>What the acting user may do to this expense right now, given role + ownership + state.</summary>
    private static IReadOnlyList<string> AvailableActions(Expense e, User actor, DataStore store)
    {
        var actions = new List<string>();
        bool isOwner = e.EmployeeId == actor.Id;
        var owner = store.Users.GetValueOrDefault(e.EmployeeId);
        bool managesOwner = actor.Role == UserRole.Manager && owner?.ManagerId == actor.Id;

        if (isOwner && e.IsEditable)
        {
            actions.Add("edit");
            actions.Add("uploadReceipt");
            if (e.Status == ExpenseStatus.Draft) actions.Add("delete");
        }

        if (isOwner && ExpenseStateMachine.CanTransition(e.Status, ExpenseStatus.Submitted, UserRole.Employee))
            actions.Add("submit");

        if (managesOwner && e.Status == ExpenseStatus.Submitted)
        {
            actions.Add("approve");
            actions.Add("reject");
        }

        if (actor.Role == UserRole.Finance && e.Status == ExpenseStatus.Approved)
            actions.Add("reimburse");

        return actions;
    }
}
