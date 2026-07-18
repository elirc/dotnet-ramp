using ExpenseManagement.Api.Background;
using ExpenseManagement.Api.Domain;
using ExpenseManagement.Api.Storage;

namespace ExpenseManagement.Api.Services;

/// <summary>
/// Orchestrates the receipt lifecycle: validate + store the file, attach it to an
/// (editable, owned) expense, then queue background processing (scan/OCR).
/// </summary>
public class ReceiptService
{
    private readonly DataStore _store;
    private readonly ReceiptStorage _storage;
    private readonly ExpenseService _expenses;
    private readonly IBackgroundTaskQueue _queue;

    public ReceiptService(
        DataStore store,
        ReceiptStorage storage,
        ExpenseService expenses,
        IBackgroundTaskQueue queue)
    {
        _store = store;
        _storage = storage;
        _expenses = expenses;
        _queue = queue;
    }

    public async Task<Receipt> UploadAsync(User actor, Guid expenseId, IFormFile file, CancellationToken ct)
    {
        var expense = _store.Expenses.GetValueOrDefault(expenseId)
            ?? throw new NotFoundException("Expense not found.");

        if (expense.EmployeeId != actor.Id)
            throw new ForbiddenException("You can only add receipts to your own expenses.");
        if (!expense.IsEditable)
            throw new DomainException($"Cannot attach a receipt to an expense in '{expense.Status}' status.");

        var receipt = await _storage.SaveAsync(expenseId, file, ct);
        _store.Receipts[receipt.Id] = receipt;

        // Replace any previous receipt on this expense.
        if (expense.ReceiptId is { } oldId && oldId != receipt.Id)
            _store.Receipts.TryRemove(oldId, out _);

        expense.ReceiptId = receipt.Id;
        expense.UpdatedAt = DateTimeOffset.UtcNow;
        _expenses.RefreshPolicy(expense);

        // Hand off scan/OCR to the background worker; policy refreshes once it completes.
        await _queue.EnqueueAsync(async (sp, token) =>
        {
            await BackgroundJobs.ProcessReceipt(receipt.Id)(sp, token);
            var svc = sp.GetRequiredService<ExpenseService>();
            if (sp.GetRequiredService<DataStore>().Expenses.TryGetValue(expenseId, out var e))
                svc.RefreshPolicy(e);
        });

        return receipt;
    }

    public Receipt GetForActor(User actor, Guid receiptId)
    {
        var receipt = _store.Receipts.GetValueOrDefault(receiptId)
            ?? throw new NotFoundException("Receipt not found.");

        // Reuse expense visibility rules to authorize file access.
        _expenses.GetForActor(actor, receipt.ExpenseId);
        return receipt;
    }

    public (Stream stream, string contentType) OpenFile(Receipt receipt) => _storage.OpenRead(receipt);
}
