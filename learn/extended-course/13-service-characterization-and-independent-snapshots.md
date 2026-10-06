# 13. Service characterization and independent snapshots

The [service lab](labs/service_lab.py) executes copied current service code with real copied domain, policy, mapping, and store classes. It uses a controlled queue and an explicitly stubbed background-job factory. This boundary is broader than the lifecycle table lab and narrower than the running application: it can prove local service ordering, but it does not execute the real worker, HTTP validation, authentication, disk storage, or UI.

```powershell
python learn/extended-course/labs/service_lab.py
```

The runner creates a disposable package-free .NET 10 project. The application remains unchanged and still targets its own framework. The lab copies twelve source files, supplies its own Program and job-factory stub, compiles, runs twenty-two baseline assertions, and runs one compiled relationship-check mutation. It hashes every copied original before and after.

## Why a characterization lab is different from a repair test

The baseline deliberately asserts the current rejection-reason mutation before an invalid transition fails. That behavior is a useful counterexample to an all-fields-unchanged contract, but the lab does not pretend the current behavior is the ideal requirement. Characterization records what happens now so a future repair can show exactly what it changes.

A repair test would instead require the old reason to remain after rejection. You cannot simultaneously claim both expectations are the unchanged contract. When implementing a repair later, retain the historical characterization as documented evidence and replace or supplement its expected behavior with a reviewed new requirement.

This distinction prevents a common testing mistake: calling every failed characterization assertion a regression even when the behavior was deliberately improved. The purpose is to make changes explicit, not to freeze every flaw forever.

## The invalid-rejection fixture

The lab creates an employee owner and a distinct manager, sets the owner's ManagerId, and stores both. It creates an Approved expense with an old reason, a known timestamp, and empty history. It then calls Reject with a different padded reason through the real copied ExpenseService.

The call throws DomainException. The baseline checks that the reason became the trimmed new value, while status, timestamp, and history remained unchanged. This executes the ordering previously predicted in chapter two. It does not rely on a mock Transition method or a copied pseudocode implementation of Reject.

The timestamp is fixed far in the past so accidental Touch would be obvious. The history check uses its count under a fixture that begins empty; a more general repair suite should copy complete history contents. The lab's narrow assertion is sufficient for this path but should not be described as a deep snapshot of every possible expense field.

## Policy refresh is observable on a failed submit

The next fixture is an owned Draft above the receipt threshold with no receipt. The lab keeps the old PolicyFlags list reference, calls Submit, expects DomainException, and checks the required-receipt blocking flag. It also verifies that the list reference changed while status stayed Draft and history stayed empty.

This establishes that a failed submission can refresh policy state even though its lifecycle transition did not occur. The test does not assume every failed command mutates something. It identifies the specific field and ordering in this specific method.

For a proposed all-fields-unchanged command contract, decide whether policy refresh is part of validation output that may legitimately change or part of the protected aggregate state that must remain stable. That decision affects both implementation and tests. A vague rollback requirement leaves this question unresolved.

## Pending receipts and severity

The lab attaches a Pending receipt to another valid Draft above the threshold. It calls Submit and observes a successful transition with a RECEIPT_PROCESSING warning and one history edge. This confirms that the selected pending-receipt branch is warning-only at this service boundary.

The receipt's filename and content type are synthetic metadata. No file is created or read, and no upload validation runs. The fixture demonstrates the store-derived receipt status and policy decision, not that the metadata corresponds to valid bytes on disk.

The expense date is yesterday relative to the lab's current UTC date. This avoids future-date and stale-date boundaries while exercising unrelated policy behavior. It is intentionally not a deterministic equality-boundary test for the ninety-day rule. Those tests still need an explicit time seam or a separately controlled fixture.

## Relationship checks and the semantic mutation

A Manager-owned Submitted expense is visible to that Manager because it is their own. The baseline expects approval to fail because the owner is not their direct report under the fixture. This is a stronger relationship test than using an unrelated manager who would fail visibility before reaching EnsureManagesOwner.

The mutation removes the direct-report condition while preserving the Manager-role check in the copied service. It compiles, then the own-expense approval no longer throws as expected. The intended semantic failure therefore demonstrates that the fixture reaches and protects the relationship boundary rather than merely relying on an earlier visibility denial.

This is an example of designing a test to distinguish adjacent guards. A poorly chosen unauthorized fixture can pass even when the guard you intended to test is removed, because another guard still rejects it. The correct fixture deliberately satisfies earlier conditions and violates only the target condition.

## Mapping hints and actual actor role

Another Manager-owned Draft is mapped through the actual copied Mappings code. The baseline sees submit in AvailableActions, then calls Submit and expects a role-based transition rejection. This executes the hint-versus-command mismatch described in chapter six.

Policy inputs are kept valid so the role rejection is reachable. If the amount required a missing receipt, the test would fail earlier for policy and would not establish the intended role distinction. This is why fixtures should isolate rejection boundaries instead of stacking unrelated invalid values.

The result does not imply that a hint must always guarantee command success. It provides concrete evidence for choosing and documenting the hint contract. A future repair may compute the actual actor role or label the hint more narrowly, but server-side checks must remain.

## Queue invocation and acceptance are separated

The queue double records EnqueueAsync invocation and returns a ValueTask backed by a pending completion source. StartReimbursement returns after changing status and history while that completion source remains incomplete. The lab checks invocation count, pending acceptance, Reimbursing status, and one history event.

The job factory is a stub because the lab never executes queued work. Its role is to provide a signature-compatible delegate so the real service can reach the queue boundary. Do not describe this as a reimbursement-worker test or a payment simulation test. The actual queue behavior was tested separately in chapter eight.

After the observations, the lab completes the gate so no artificial pending task remains. It does not push work into a real host or leave a background process running.

## Direct service calls reveal validation boundaries

The final fixture calls Create directly with padded merchant, description, and currency. Merchant and description are trimmed. Currency is uppercased but retains spaces. This input would not necessarily pass the ordinary HTTP DTO length constraints, so the result is explicitly a direct-service characterization.

This is a useful reminder that service methods do not automatically execute attribute validation. A test must say whether it entered through the controller, invoked validation explicitly, or called the service directly. Otherwise a reader can mistake a deliberately bypassed boundary for an endpoint behavior.

## Independent exercises

Exercise NR13-A: explain why the own-Manager approval fixture reaches the relationship guard while an unrelated Manager may fail earlier. Design a second fixture that isolates ownership from role.

Exercise NR13-B: classify all twenty-two assertions as current characterization, authorization protection, mapping consistency evidence, or handoff ordering. Identify which would intentionally change under a reviewed rejection-atomicity repair.

Exercise NR13-C: extend the snapshot plan from scalar checks to complete history and policy contents. State why the stub job factory is acceptable for this boundary and which worker claims it cannot support.
