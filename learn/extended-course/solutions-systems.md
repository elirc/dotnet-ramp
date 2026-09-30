# Systems solutions: receipts, reporting, and policy

Use this guide after completing chapters five through seven. The answers identify current source behavior and keep unexecuted experiments separate. They extend the foundation assessment without replacing its independent capstone.

## NR05-A: replacement across four resources

The new receipt's bytes are written before ReceiptStorage returns its metadata object. ReceiptService inserts the new metadata, removes the old receipt metadata when replacing a different receipt, assigns the expense's ReceiptId, updates the timestamp, refreshes policy, and awaits enqueue acceptance. The inspected path does not delete the old stored file.

A correct worksheet therefore distinguishes removal from the receipt dictionary from removal from disk. It also distinguishes a queued delegate from completed processing. A proposed cleanup process needs a way to identify unreferenced files and avoid deleting a file still associated with the current expense. That proposal requires its own consistency and failure tests.

If the old job is still queued when a replacement occurs, its receipt lookup may find that the old metadata was removed and return. The wrapper later refreshes the current expense if it exists. The current receipt reference, not the historical closure's reference, determines the policy evaluation. A concurrency experiment should record both identifiers so this behavior remains visible.

## NR05-B: validation table

The controller's fixed request-size limit applies to the HTTP request envelope. Storage's configured maximum applies to the declared file length. Storage rejects empty files, oversized files, unsupported content types, and mismatched extensions according to the selected allowlist. It does not inspect actual file signatures or decode content in the shown method.

A file exactly at the configured maximum passes the greater-than size guard, assuming the other checks pass. A zero-length file fails before type checks. A matching JPEG content type and accepted JPEG extension can pass metadata validation, but that does not prove the bytes contain a valid image. A proposed content-inspection experiment should use synthetic bytes in a disposable root and assert both rejection and cleanup behavior.

A configuration review should compare the per-file cap with the controller's request limit. Increasing one value does not automatically increase the other. Tests should isolate guards so an earlier size failure does not masquerade as evidence that extension validation was exercised.

## NR05-C: cancellation and processing

A controlled IFormFile can write a small prefix and throw during CopyToAsync. The expected storage outcome should be characterized in an owned temporary directory, with original application paths untouched. The selected code disposes the stream but shows no explicit cleanup of a partially created file on that failure path. The service has not yet received metadata to insert.

The request token reaches file copying. EnqueueAsync has no token parameter in the selected interface, and the queued delegate receives the hosted worker's stopping token. Awaiting enqueue observes acceptance, while awaiting the worker delegate would observe processing. The upload path does the former before returning; the worker performs the latter later.

## NR06-A: summary arithmetic

For the six-status fixture with amounts ten, twenty, thirty, forty, fifty, and sixty, TotalSubmitted is twenty. TotalApproved is seventy because it includes Approved and Reimbursing. TotalReimbursed is fifty. PendingApprovalCount is one. Category and status breakdowns include the whole visible date-filtered set, whose amount sum is two hundred ten under the fixture.

An unrelated employee's row is excluded for Erin and for a manager without the required visibility relationship, but can enter Finance's selected view. The test must supply explicit actor and relationship data. A total alone does not reveal which visibility scope produced it.

## NR06-B: units and snapshots

The current report sums decimal Amount without grouping by Currency. A per-currency proposal could return separate USD and EUR totals and prohibit adding them into an unlabeled grand total. A conversion proposal would need rate provenance, effective date, rounding, and stored evidence. A made-up exchange rate is not an adequate repair contract.

ConcurrentDictionary protects its individual operations, not an immutable snapshot of every property on its mutable values. A controlled snapshot experiment can pause between projection reads while another actor changes a field, then compare the result with the chosen consistency contract. Do not call this an observed runtime defect until such an experiment actually runs.

## NR06-C: action hints

For an owned Manager Draft, AvailableActions can include submit because it checks the Employee transition for an owner. Submit later passes the actual actor role to Transition, so the Manager role can fail that edge after policy refresh, assuming no blocking policy rejection occurs first. This is a specific source-derived mismatch between hint calculation and command eligibility.

One proposed contract describes actions as role-and-state hints that may still fail policy or concurrency checks. Another computes a more precise eligibility object with reasons. Either way, server-side ownership, relationship, policy, and transition checks remain necessary because the response can be stale and the client is not an authorization boundary.

## NR07-A: amount ladders

A negative amount and zero produce AMOUNT_INVALID with blocking severity; a positive cent avoids that rule. Missing receipt triggers RECEIPT_REQUIRED only above twenty-five. Category warnings trigger only above the selected category limit, and HIGH_VALUE only above one thousand. Equal values do not satisfy these strict greater-than comparisons.

For Software just above one thousand with no receipt and otherwise valid fields, expect the missing-receipt blocking flag plus category-limit and high-value warnings. For Meals above its smaller soft limit but below the receipt threshold, choose a value carefully: Meals' limit is higher than twenty-five, so such a combination is impossible. Recognizing an impossible fixture is part of good boundary design. A Meals example above its category limit necessarily exceeds the receipt threshold, so attach a processed receipt to isolate the warning.

## NR07-B: receipt derivation

An absent ReceiptId above the threshold produces the required-receipt blocking flag. A dangling nonnull identifier yields hasProcessedReceipt false when RefreshPolicy cannot find metadata and follows the attached-but-unprocessed warning branch in Evaluate. Pending and failed receipts also do not satisfy the processed-status check. A processed receipt does.

Under otherwise valid inputs, warning-only cases do not make HasBlocking true. A pure engine test can supply the boolean directly, while a RefreshPolicy test establishes its derivation from the current store. The latter is needed to prove how dangling or failed metadata affects the service path.

## NR07-C: time and changed requirements

A pure evaluation core accepting DateOnly today is a small deterministic seam. A wrapper can read the real clock and pass the date, while tests supply exact reference dates. A clock abstraction is another option if several services need coordinated time. Choose the seam for clarity and scope, not to maximize abstraction count.

The test plan should separate pure threshold decisions, store-derived receipt status, and HTTP presentation. A proposed stale rule of ninety days or older changes the equality row from the current strictly older-than rule. Its acceptance criteria should show that one changed row and preserve unrelated amount and receipt decisions. Calling the change a refactor would conceal a user-visible policy difference.

## Review rubric for the systems extension

Award ten points per exercise for a maximum of ninety: four for correct current-source prediction, three for a reproducible fixture or calculation, and three for precise evidence limits or proposed-contract reasoning. Do not award full fixture credit to tests that vary several unrelated guards without explaining rejection order.

A strong extension submission connects the chapters. It notices that receipt processing changes policy inputs, that report projections can observe mutable values, and that action hints do not guarantee a later command succeeds. It uses those connections to choose a small next experiment rather than proposing a wholesale rewrite without evidence.


## NR08-A: capacity-one timeline

The first enqueue is invoked and accepted while the queue is empty. The second enqueue is invoked while the first item occupies the only slot, so its acceptance remains pending. Dequeuing the first item frees capacity; the second writer can then complete acceptance, and a later dequeue returns its delegate. Neither delegate has to run for this handoff sequence to occur.

The test compares delegate references so it can detect substitution or ordering mistakes. Its five-second watchdog bounds a failure after capacity should become available; it does not use elapsed time as the synchronization proof. The pending-state assertion occurs before the dequeue that enables progress.

## NR08-B: semantic mutation and evidence

DropWrite changes the full-queue behavior, so the second enqueue can complete instead of waiting. The intended failing assertion is full queue waits for capacity. The mutation must compile before this failure counts as useful evidence. A build error would only show a broken experiment.

The seven baseline assertions cover acceptance, retrieval identity, one cancellation case, and null input. They do not execute delegates, construct dependency-injection scopes, test hosted-worker survival, or persist accepted work. A successful queue operation is therefore not equivalent to completed or durable business work.

## NR08-C: shutdown proposal

A graceful-drain proposal might stop accepting new producers, allow the running job and queued work to finish until a deadline, then record or cancel remaining work according to an explicit policy. A fast-stop proposal might cancel the running job and abandon in-memory queued items. Both need a clear caller outcome for the waiting producer.

The selected interface has no completion operation or enqueue cancellation token, so a stronger shutdown protocol may need additional behavior. A controlled test should gate the running job, occupy the queue, start a waiting producer, trigger shutdown, and record all three outcomes. No such host-level experiment was executed by the queue lab.

Score these three additional exercises out of ten each using the systems rubric, adding thirty points to the ninety-point chapters-five-through-seven extension.


## NR09-A: token resolution

An unknown token returns no user without finding a session to remove. An expired token causes its session entry to be removed and returns no user. Logout removes the token entry, so later validation follows the unknown-token path. A valid unexpired session resolves the current user by identifier; if that user is missing, validation returns no user rather than an embedded stale user snapshot.

PasswordHasher's fixed-time byte comparison is only one step. Login short-circuits for missing users and includes lookup, decoding, and derivation work. Therefore, the selected code does not establish constant-time behavior for the entire endpoint. Functional tests should use synthetic passwords and avoid recording credential material.

## NR09-B: request context

An absent or malformed NameIdentifier claim produces no parsed UserId, and RequireUser throws its unauthenticated forbidden exception. A valid GUID with no matching user reaches the store lookup and throws the missing-user forbidden exception. A valid GUID with a present user returns that current object.

These fixtures prove context resolution for a supplied principal. They do not prove that the real authentication handler creates trustworthy claims or that authorization middleware always invokes the controller correctly. A separate HTTP test must establish the surrounding pipeline behavior.

## NR09-C: response paths

Explicit login failure returns the controller's Unauthorized result and selected generic message. A DomainException caught by the middleware maps to 422 with its business-rule title, message detail, and optional details extension. An unexpected exception maps to 500 with a generic response detail and server-side error logging.

A bounded middleware test can throw a synthetic marker-bearing exception and assert the marker is absent from the serialized 500 response. It should also verify content type and status. A response-already-started scenario needs its own host or middleware experiment because the selected code shows no guard before writing the replacement problem response. Do not claim that scenario was covered by the queue or lifecycle labs.

These three exercises add thirty points using the same ten-point-per-exercise rubric. The available route now has twenty-four prediction exercises, plus its separate foundation capstone.
