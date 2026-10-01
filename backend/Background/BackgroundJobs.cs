using ExpenseManagement.Api.Domain;
using ExpenseManagement.Api.Storage;

namespace ExpenseManagement.Api.Background;

/// <summary>
/// Factory methods for the background jobs in this app. They are plain delegates
/// so the queue stays generic; each resolves what it needs from the scope.
/// </summary>
public static class BackgroundJobs
{
    /// <summary>
    /// Simulates antivirus scan + OCR on an uploaded receipt: after a short delay
    /// the receipt is marked Processed and an "extracted" amount is recorded.
    /// </summary>
    public static Func<IServiceProvider, CancellationToken, Task> ProcessReceipt(Guid receiptId) =>
        async (sp, ct) =>
        {
            var store = sp.GetRequiredService<DataStore>();
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("ReceiptProcessing");

            await Task.Delay(TimeSpan.FromSeconds(2), ct); // pretend work

            if (!store.Receipts.TryGetValue(receiptId, out var receipt))
                return;

            // Simulated OCR: echo the expense amount so the UI can show a cross-check.
            var expense = store.Expenses.GetValueOrDefault(receipt.ExpenseId);
            receipt.ExtractedAmount = expense?.Amount;
            receipt.Status = ReceiptStatus.Processed;

            logger.LogInformation("Receipt {ReceiptId} processed.", receiptId);
        };

    /// <summary>
    /// Simulates issuing a reimbursement payment: after a delay the expense moves
    /// Reimbursing -> Reimbursed and gets a payment reference. This is a
    /// system-triggered state transition (no human actor).
    /// </summary>
    public static Func<IServiceProvider, CancellationToken, Task> ProcessReimbursement(
        Guid expenseId, TimeSpan? processingDelay = null) =>
        async (sp, ct) =>
        {
            var store = sp.GetRequiredService<DataStore>();
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Reimbursement");

            await Task.Delay(processingDelay ?? TimeSpan.FromSeconds(3), ct); // pretend payment rails

            lock (store.GetExpenseLock(expenseId))
            {
                if (!store.Expenses.TryGetValue(expenseId, out var expense))
                    return;

                // The gate makes duplicate jobs harmless: only the first job can
                // observe Reimbursing and append the payment event.
                if (!ExpenseStateMachine.CanSystemTransition(expense.Status, ExpenseStatus.Reimbursed))
                {
                    logger.LogWarning("Expense {ExpenseId} not in a reimbursable state ({Status}).",
                        expenseId, expense.Status);
                    return;
                }

                var from = expense.Status;
                expense.PaymentReference = $"PMT-{DateTime.UtcNow:yyyyMMdd}-{expense.Id.ToString()[..8].ToUpperInvariant()}";
                expense.Status = ExpenseStatus.Reimbursed;
                expense.UpdatedAt = DateTimeOffset.UtcNow;
                expense.History.Add(new ExpenseEvent(
                    from, ExpenseStatus.Reimbursed,
                    ExpenseStateMachine.SystemActorId, "System",
                    $"Payment issued ({expense.PaymentReference}).",
                    DateTimeOffset.UtcNow));

                logger.LogInformation("Expense {ExpenseId} reimbursed ({Ref}).", expenseId, expense.PaymentReference);
            }
        };
}
