# 17. Receipt replacement interleavings and recovery

Read [ReceiptService](../../backend/Services/ReceiptService.cs), [ReceiptStorage](../../backend/Services/ReceiptStorage.cs), [BackgroundJobs](../../backend/Background/BackgroundJobs.cs), and [ExpenseService](../../backend/Services/ExpenseService.cs). This advanced module turns the receipt lifecycle into controlled interleavings. The scenarios are proposed experiments based on current source; they are not claims that every race has occurred in the running application.

## Name the resources and owners

A replacement upload involves a disk file, receipt metadata, the expense's current receipt pointer, a queued job, and derived policy flags. The request owns the upload attempt, but the worker later owns processing. The expense pointer identifies which receipt is current; an older queued job can still exist after that pointer changes.

Use explicit identifiers E, R1, R2, and J1 rather than phrases such as the receipt. Ambiguous language hides whether a step acts on the old or new object. A timeline should state which identifier each lookup uses and whether it reads a value captured earlier or the current store state.

The upload wrapper captures receipt.Id and expenseId. The processing job looks up the captured receipt identifier. After processing, the wrapper looks up the current expense by captured expenseId and refreshes policy using that expense's current ReceiptId. This combination allows old work and current policy to interact without necessarily processing the current receipt.

## Interleaving one: replace before old processing starts

Begin with E pointing to R1 and J1 queued. Upload R2. The selected replacement path inserts R2 metadata, removes R1 metadata, and points E to R2 before queuing J2. When J1 later runs, its receipt lookup can find no R1 and return. The wrapper can still refresh E's current policy afterward if E exists.

A controlled test should record that J1 did not mark R2 processed merely because it refreshed E. The bare job uses R1's identifier. RefreshPolicy then evaluates R2's actual status. If R2 remains Pending, the expected flag remains the processing warning under the selected policy.

This scenario separates processing identity from policy identity. A test that checks only final expense flags can miss whether the wrong receipt object was processed. Include receipt statuses and job identifiers in the observation table.

## Interleaving two: old job holds a reference

A different schedule can let J1 obtain R1 before the replacement removes its dictionary entry. Because the receipt is a mutable object, the worker can still hold that reference even after the map no longer exposes it. It may mark that detached object processed while E now points to R2.

The exact placement of awaits and delays in the real job matters. The current job delays before its lookup, so a fixture must pause at an actual reachable boundary or introduce a declared test seam rather than assume a hidden yield exists between every statement. This is a critical rule for concurrency labs: invented scheduling points can demonstrate an impossible interleaving.

A future persistent adapter changes the mechanics again because a fetched row may be a detached value rather than a shared object. The test should target the chosen storage semantics and not transfer in-memory reference behavior without review.

## Interleaving three: two replacements compete

Suppose two uploads both pass ownership and editable-state checks for E, then store different files. They can later update metadata and the current pointer in an order influenced by asynchronous file operations and queue waits. Without a version or lock covering the whole replacement decision, the meaning of last replacement needs a defined linearization point.

Choose a proposed contract: the latest accepted command version wins, or the last completed pointer assignment wins, or concurrent replacement is rejected. Each can be tested, but they imply different user messages and cleanup behavior. A browser busy flag is not sufficient because two requests can come from different clients.

The worksheet should include both files even if only one receipt remains current. Losing the pointer race does not automatically remove the losing file or metadata. A cleanup design must know whether that resource is unreferenced, still processing, or retained for audit.

## A durable receipt attempt can clarify ownership

A proposed receipt-attempt record can contain attempt ID, expense ID, expected version, file location, metadata ID, processing state, and cleanup state. The record gives recovery code a durable place to decide what happened after a crash. This is a design extension; the current implementation uses dictionaries and local files without such an attempt record.

Do not store only a vague failed flag. Distinguish file copy incomplete, file stored but unattached, attached and waiting for processing, processed, superseded, and cleanup pending. These states explain which actions are safe. Retrying processing differs from deleting an unattached file or retrying pointer attachment.

The record also needs an ownership or lease rule if several workers can recover it. A single process can still crash between operations, so explicit state helps even before multi-worker concurrency is introduced.

## Compensating cleanup is not rollback

If file storage succeeds but metadata attachment fails, deleting the new file can compensate for an orphan. That deletion is another operation that can fail. Calling the whole sequence transactional would hide the fact that compensation can be delayed or incomplete.

A robust proposal records cleanup intent and retries it idempotently. The cleanup worker should verify that the file remains unreferenced before deletion and operate only within its configured owned storage root. Educational tests should use synthetic temporary files and verify path containment; they should never clean a broad user directory.

If the file must be retained for audit, compensation may mean marking it unlinked rather than deleting it. The retention requirement belongs in the product contract. Do not choose deletion merely because it simplifies a unit test.

## Processing status and derived policy can lag

The receipt job updates receipt status, and the wrapper refreshes expense policy afterward. Between those steps, a reader can potentially observe processed metadata with stale policy flags under the mutable in-memory model. Whether that interval is reachable and visible should be established by a controlled seam or integration test.

A proposal can compute policy on read, update receipt and expense under a shared transaction, or accept eventual consistency with a freshness indicator. Each has costs. Computing on read changes report and response work; transactional update requires a persistence boundary; eventual consistency needs clear UI behavior.

The detail page currently polls while receipt status is Pending or expense status is Reimbursing. If receipt status becomes Processed before policy refresh is reflected in the returned snapshot, the polling condition may stop under a particular interleaving. This is a cross-layer hypothesis to test, not a demonstrated browser bug. It shows why status and derived-field consistency matter to recovery.

## Build a deterministic interleaving harness

Use gates at real asynchronous boundaries: file copy completion, queue acceptance, job delay replacement, and repository calls in a proposed adapter. Name each gate and record which operation owns it. Avoid sleeping for guessed milliseconds and hoping the desired order occurs.

For each schedule, write a legal sequence before running it. If the source has no await or synchronization seam at the desired point, explain whether the test uses a copied-source instrumentation seam and what that changes. Do not claim an instrumented model is an unmodified application execution.

The oracle should inspect current pointer, all receipt records, file existence, job outcomes, policy codes, and cleanup records if proposed. A final success response alone cannot reveal orphan resources or stale derived state.

## Independent exercises

Exercise NR17-A: trace old-job-after-replacement using explicit R1 and R2 identities. Identify which receipt the job processes and which receipt RefreshPolicy evaluates.

Exercise NR17-B: define a concurrent replacement contract and its linearization point. Design a two-upload schedule with controlled file completion and state every resource that may require cleanup.

Exercise NR17-C: propose receipt-attempt states and a compensating cleanup protocol. Include one failed cleanup and one policy-refresh lag case, with precise current-versus-proposed labels.
