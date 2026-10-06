# Domain Glossary

The nouns and verbs of this system, and exactly where each lives. When a PR or a teammate
says "the actor," "a blocking flag," or "a system transition," this is what they mean.

| Term | Meaning in this repo | Defined / used at |
| --- | --- | --- |
| **Expense** | A single spend claim moving through a lifecycle | [`Expense.cs` L15-45](../../../backend/Domain/Expense.cs#L15-L45) |
| **Actor** | The authenticated user performing the current action | passed as `User actor` everywhere, e.g. [`ExpenseService.Submit` L114](../../../backend/Services/ExpenseService.cs#L114) |
| **Role** | `Employee` \| `Manager` \| `Finance` — what you may do | [`UserRole` enum](../../../backend/Domain/Enums.cs) |
| **Status** | Where an expense is in its lifecycle | [`ExpenseStatus` enum](../../../backend/Domain/Enums.cs) |
| **Transition** | A status change, validated + recorded in one place | [`ExpenseService.Transition` L169-179](../../../backend/Services/ExpenseService.cs#L169-L179) |
| **State machine** | The table of legal transitions and which role triggers each | [`ExpenseStateMachine` L13-59](../../../backend/Domain/ExpenseStateMachine.cs#L13-L59) |
| **System transition** | A status change with no human actor (the worker reimbursing) | [`CanSystemTransition` L57-58](../../../backend/Domain/ExpenseStateMachine.cs#L57-L58), used at [`BackgroundJobs` L51](../../../backend/Background/BackgroundJobs.cs#L51) |
| **Policy flag** | A `(Code, Message, Severity)` result of one policy rule | [`PolicyFlag` L14](../../../backend/Domain/Expense.cs#L14) |
| **Blocking vs Warning** | Blocking stops submission; Warning is recorded for the approver | [`PolicySeverity`](../../../backend/Domain/Enums.cs); enforced at [`Submit` L119-126](../../../backend/Services/ExpenseService.cs#L119-L126) |
| **Direct report** | An employee whose `ManagerId` is the manager's id | [`IsDirectReport` L204-205](../../../backend/Services/ExpenseService.cs#L204-L205) |
| **Owner** | The employee who created an expense (`EmployeeId`) | [`GetOwned` L181-188](../../../backend/Services/ExpenseService.cs#L181-L188) |
| **Available actions** | Server-computed list of what the actor may do to an expense | [`Mappings.AvailableActions` L45-72](../../../backend/Services/Mappings.cs#L45-L72) |
| **History / audit trail** | Append-only list of every transition on an expense | [`Expense.History` L42](../../../backend/Domain/Expense.cs#L42) |
| **Receipt** | An uploaded file attached to an expense, processed async | [`Receipt.cs`](../../../backend/Domain/Receipt.cs) |
| **Extracted amount** | The (simulated) OCR amount, for cross-checking | [`Receipt.ExtractedAmount`](../../../backend/Domain/Receipt.cs); set at [`BackgroundJobs` L29](../../../backend/Background/BackgroundJobs.cs#L29) |
| **Payment reference** | The id stamped when reimbursement completes | set at [`BackgroundJobs` L59](../../../backend/Background/BackgroundJobs.cs#L59) |
| **Session / token** | Opaque bearer string mapped to a user in memory | [`Session` record](../../../backend/Storage/DataStore.cs); issued at [`AuthService.Login` L24-36](../../../backend/Security/AuthService.cs#L24-L36) |
| **DataStore** | The in-memory "database" (a singleton of dictionaries) | [`DataStore.cs`](../../../backend/Storage/DataStore.cs) |
| **DomainException** | "You broke a business rule" → HTTP 422 | [`DomainExceptions.cs`](../../../backend/Domain/DomainExceptions.cs) |
| **ForbiddenException / NotFoundException** | Authorization / existence failures → 403 / 404 | [`DomainExceptions.cs`](../../../backend/Domain/DomainExceptions.cs) |

## Lifecycle at a glance

```
Draft ──submit(Employee)──▶ Submitted ──approve(Manager)──▶ Approved ──reimburse(Finance)──▶ Reimbursing ──(system)──▶ Reimbursed
  ▲                             │
  │                             └──reject(Manager)──▶ Rejected ──submit(Employee)──▶ Submitted
  └── edit while Draft or Rejected (IsEditable)
```

`Reimbursed` is the only terminal state ([`IsTerminal` L45](../../../backend/Domain/ExpenseStateMachine.cs#L45)).
