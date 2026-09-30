# 01. Authority, lifecycle edges, and service boundaries

This foundation route begins with the current expense workflow, not a historical incident branch. Read [ExpenseStateMachine](../../backend/Domain/ExpenseStateMachine.cs), [ExpenseService](../../backend/Services/ExpenseService.cs), and [Expense](../../backend/Domain/Expense.cs). Record predictions before opening the separate solutions. The course explains observed source behavior and proposed improvements separately; it does not modify application code.

The immediate goal is to follow one expense from draft through submission, approval, reimbursement request, and background completion. The deeper goal is to distinguish a legal lifecycle edge from an authorized action on a particular expense. Those questions use different information and are answered by different methods.

## Draw the graph before reading the service

The state table contains Draft to Submitted for Employee, Rejected to Submitted for Employee, Submitted to Approved or Rejected for Manager, Approved to Reimbursing for Finance, and Reimbursing to Reimbursed with an empty human-role array. Reimbursed is terminal. Rejected is editable and has an outgoing resubmission edge; it is not terminal merely because its name sounds final.

CanTransition requires both an existing edge and membership of the supplied role in that edge's array. CanSystemTransition instead requires only that the edge exists. It does not require the human-role array to be empty. Therefore, the system predicate currently returns true for Draft to Submitted as well as Reimbursing to Reimbursed. The reimbursement worker chooses a specific destination, which narrows how that broad predicate is used on its current path.

Do not silently reinterpret CanSystemTransition as only system-only edges. A proposed stricter helper would be a policy change, and its effect on callers should be reviewed. Conversely, the broad helper does not mean the current worker can reimburse directly from any state: its destination is Reimbursed, and the table contains that destination only from Reimbursing.

## Add ownership and reporting relationships

The state table knows a role but not the actor's identity, expense owner, or manager relationship. ExpenseService supplies those checks. GetOwned requires the expense's EmployeeId to equal the actor's identity. GetForActor applies a visibility policy: employees see their own, managers see their own and direct reports, and Finance can see all under the selected implementation.

Approve and Reject first obtain a visible expense, then require a Manager who manages that owner, and finally request the lifecycle transition. A manager's ability to see an expense is therefore not identical to permission to approve it. For example, visibility of one's own expense does not by itself satisfy the direct-report approval condition.

Use three identities in a worksheet: employee Erin, manager Morgan, and Finance actor Finley. Give Erin a ManagerId pointing to Morgan. Use another employee, Devon, reporting elsewhere. A role-only test cannot distinguish Morgan acting on Erin from Morgan acting on Devon. The service fixture must include the relationship data to test that boundary.

## Trace a successful submission

Submit obtains the owned expense and refreshes policy flags before checking for blocking flags. If blocking flags exist, it gathers their messages and throws a domain exception. Otherwise it calls Transition to Submitted. Transition checks the role-and-edge predicate, captures the old status, assigns the new status, updates the timestamp, and appends an ExpenseEvent with actor and note.

For an employee-owned Draft with valid policy inputs, predict one new history entry from Draft to Submitted. The actor identity comes from the supplied User, not from a free-form request field in this service method. The history timestamp and UpdatedAt are separate UtcNow reads, so exact equality between them is not guaranteed by a shared captured instant.

The policy refresh occurs before lifecycle validation. A caller trying to submit an already approved expense can therefore reach policy work before the transition rejects. That matters when defining unchanged-state guarantees. An exception alone does not establish that PolicyFlags retained the same list or values.

## Inspect comments as hypotheses

ExpenseStateMachine's comment describes a single source of truth, while Expense exposes a public settable Status. ExpenseService centralizes its own transition method, but the background reimbursement job also assigns status and history directly after a system-edge check. These facts refine the comment's scope. The table is a shared policy source used by these paths, not a language-enforced prohibition on every other assignment.

This is not an invitation to rewrite the whole model during a reading exercise. First inventory the actual writers and their guards. Then decide whether a stronger encapsulation proposal is warranted. A private setter, aggregate method, or richer command API can change serialization, storage, and fixture construction, so the design should account for those consequences.

The same caution applies to append-only history. The current property is a mutable List with a public setter. The intended service behavior appends records, but the type itself does not make arbitrary replacement impossible. Distinguish an application convention from an enforced invariant when reviewing or teaching the model.

## Build an authority matrix

Use rows for owner Employee, different Employee, direct-report Manager, unrelated Manager, and Finance. Use columns for list visibility, get visibility, update ownership, approve relationship, and lifecycle role. Fill each cell by tracing the relevant method rather than assuming a single allow or deny value applies to every operation.

A Finance actor can view an expense yet fail a transition intended for Manager. An owner with a non-Employee role can pass GetOwned yet fail the Employee-only submission edge. These cases show why identity and role are not substitutes. They also make good regression fixtures because a simplistic role shortcut changes an observable result.

Keep endpoint authentication outside this first matrix. The service accepts a User object; whether a real request receives the correct User depends on authentication and context wiring. A direct service test can establish its policy for a supplied actor without proving the HTTP pipeline's identity assurance.

## Independent exercises

Exercise NR01-A: write the six legal graph edges, their human roles, and CanSystemTransition results. Explain why Rejected and Reimbursed differ, and why the broad system predicate does not let the current reimbursement worker skip approval.

Exercise NR01-B: build the five-row authority matrix with distinct identities and manager relationships. Include a visible-but-not-approvable expense and an owned-but-role-ineligible submission. Name the first rejecting boundary for each.

Exercise NR01-C: critique the phrases single source of truth and append-only history using the actual property and writer shapes. Propose one stronger invariant boundary, identify a compatibility question, and keep the proposal separate from current behavior.

The chapter is complete when you can predict both the lifecycle decision and the object-specific authorization path. Continue with the failure trace, then use the bounded table lab and capstone to test the independence of your reasoning.
