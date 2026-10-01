namespace ExpenseManagement.Api.Domain;

/// <summary>Who a user is and what they are allowed to do.</summary>
public enum UserRole
{
    /// <summary>Creates and submits their own expenses.</summary>
    Employee,
    /// <summary>Approves/rejects expenses for their direct reports.</summary>
    Manager,
    /// <summary>Processes reimbursements for approved expenses.</summary>
    Finance
}

/// <summary>
/// Lifecycle of an expense. Transitions are governed by <see cref="ExpenseStateMachine"/>.
/// </summary>
public enum ExpenseStatus
{
    Draft,
    Submitted,
    Approved,
    Rejected,
    /// <summary>Reimbursement queued and being processed in the background.</summary>
    Reimbursing,
    Reimbursed
}

public enum ExpenseCategory
{
    Meals,
    Travel,
    Lodging,
    Transportation,
    OfficeSupplies,
    Software,
    Entertainment,
    Other
}

/// <summary>Background processing state of an uploaded receipt (scan + OCR).</summary>
public enum ReceiptStatus
{
    Pending,
    Processed,
    Failed
}

/// <summary>
/// A blocking flag stops submission; a warning is recorded for the approver to review.
/// </summary>
public enum PolicySeverity
{
    Warning,
    Blocking
}
