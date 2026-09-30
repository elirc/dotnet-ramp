# 05. Receipt files, metadata, and background policy refresh

The receipt path crosses local disk, in-memory metadata, a mutable expense, and a background queue. Read [ReceiptStorage](../../backend/Services/ReceiptStorage.cs), [ReceiptService](../../backend/Services/ReceiptService.cs), [ReceiptsController](../../backend/Controllers/ReceiptsController.cs), and [Receipt](../../backend/Domain/Receipt.cs). This chapter develops a consistency model for those distinct resources. It does not upload any real document or alter storage configuration.

## Separate the four resources

A receipt is not only a file. The selected flow creates bytes on disk, a Receipt object in the receipt dictionary, an expense's ReceiptId reference, and a queued processing delegate. Each resource has a different lifetime and failure boundary. A useful worksheet gives each its own column instead of writing a single uploaded flag.

Start with an editable expense owned by Erin and no receipt. UploadAsync first finds the expense, checks ownership, and checks editable state. It then awaits file storage. Only after storage returns does it insert receipt metadata, remove old metadata if replacing another receipt, assign the expense's new receipt reference, update its timestamp, and refresh policy.

Finally it awaits EnqueueAsync for a wrapper delegate. That is different from StartReimbursement, whose selected method invokes enqueue without awaiting the returned ValueTask. The difference is a concrete reason to inspect each caller rather than treating all uses of the same queue interface as equivalent.

Awaiting enqueue means the upload path observes queue acceptance completion. It does not mean receipt processing has completed. The queued wrapper runs later, awaits the processing job, and refreshes policy on the current expense if it still exists. Keep accepted and processed as separate statuses in both tests and user-facing explanations.

## Read file validation literally

ReceiptStorage rejects zero-length files and files whose declared length exceeds the configured maximum. The default maximum is five mebibytes. It allows selected content types for JPEG, PNG, and PDF and cross-checks the original filename extension against the corresponding extension, with a JPEG extension variation.

The selected code checks client-provided metadata; it does not inspect magic bytes or decode the content to prove the file is a valid image or PDF. Therefore, the phrase allowed file type should be explained as the implemented metadata checks, not a complete content authenticity guarantee. A proposed content-inspection step would be an additional design choice with its own error and resource limits.

The controller also sets a request-size limit of six mebibytes. A multipart request contains more than the file bytes alone, so request and file limits serve different purposes. The storage limit is configurable while the selected controller attribute is a fixed value. A configuration change can therefore alter their relationship. Do not assume every configured per-file limit remains below the request envelope without checking deployment settings.

Boundary fixtures should isolate one guard at a time: zero length, exact maximum, one byte over maximum, allowed content type with matching extension, and allowed content type with mismatched extension. A test using an oversized mismatched file cannot tell which guard would reject the mismatch because the size guard runs first.

## Server names and display names have different roles

The stored filename is generated from a GUID plus the allowlisted extension. The original client filename is reduced to its filename component for display metadata. The disk path is constructed using the generated stored name, not the display name. This separation reduces the influence of client filename text on the write location.

Do not broaden that observation into a guarantee about every possible future Receipt object. StoredFileName is a public settable property, and OpenRead accepts a Receipt. The read method includes a defensive normalized-path prefix check and an existence check. A design review should evaluate the actual containment semantics, including path boundaries and platform comparison behavior, rather than assume any textual prefix test is a complete directory-containment proof.

For this course, keep such a review inside a disposable temporary directory with synthetic files if you choose to execute it. There is no need to read outside the lab directory or use real receipts. The pedagogical goal is to define a path-containment contract and test it safely, not to demonstrate access to unrelated data.

## Trace a replacement

Suppose expense E points to receipt R1 and the receipt dictionary contains R1. A replacement upload writes a new file and returns R2. The service inserts R2, removes R1 from the receipt dictionary, updates E to point to R2, refreshes policy, and enqueues processing for R2.

The inspected replacement path removes old metadata but does not call a file-deletion method for R1's stored file. Expense deletion likewise removes receipt metadata in the inspected service without a demonstrated storage cleanup call. This supports a source-level cleanup question: what lifecycle removes unreferenced bytes? It does not establish a measured disk leak or its operational size.

A proposed cleanup design must account for timing. Deleting the old file before the new reference is safely established can leave the expense without usable evidence if a later step fails. Deferring deletion avoids that immediate loss but needs a reliable orphan-discovery or cleanup record. A local filesystem and in-memory dictionaries do not share a database transaction automatically.

## Cancellation can interrupt different phases

The upload cancellation token is passed to file copying. If copying is canceled or fails, the FileStream is disposed, but the selected method does not show deletion of a partially created file in a catch or finally block. The metadata return is not reached, so the service does not receive a Receipt to insert on that path.

This suggests a controlled failure test using a fake IFormFile whose copy operation writes a small prefix and then throws. The test should use an owned temporary storage root, assert the exact file and metadata outcome, and clean up only that root. That is a proposed lab, not an executed check in the current course report.

After the file and metadata changes, enqueue can wait for capacity. The queue interface shown does not accept the upload cancellation token as an enqueue argument. The queued job later receives the hosted worker's stopping token. Request cancellation and worker shutdown are therefore different token paths, and a diagram should label them separately.

## Processing and policy refresh are a wrapper contract

BackgroundJobs.ProcessReceipt simulates work, looks up the receipt, copies the expense amount when available, and marks the receipt processed. The upload service wraps that job and then asks ExpenseService to refresh policy on the expense if it still exists. The refresh is not inside the bare processing delegate itself.

That distinction matters if another caller invokes ProcessReceipt directly. The caller would need to establish whether policy refresh is required and where it occurs. It also matters to tests: a test of the bare job can pass while a wrapper bug leaves policy flags stale. Choose the test boundary according to the contract you want to establish.

If the expense disappears before processing, the job and wrapper have separate existence checks. If the expense has a different receipt by the time the wrapper refreshes, RefreshPolicy evaluates the current ReceiptId rather than the original closure's receipt identity. A useful concurrency proposal records both identities and defines whether stale work should update only its own metadata or influence current expense policy.

## Access to a receipt follows expense visibility

GetForActor obtains the receipt and delegates authorization to ExpenseService.GetForActor using the receipt's ExpenseId. The controller obtains the current actor, calls this method, and only then opens the file. The selected download path therefore reuses expense visibility rather than a separate receipt-owner rule.

A fixture should include an employee owner, direct-report manager, unrelated manager, and Finance actor. It should also include a receipt whose associated expense is missing. The expected response mapping belongs to middleware or host tests; the service-level prediction is about which lookup or authorization exception occurs first.

## Independent exercises

Exercise NR05-A: trace replacement of R1 with R2 across disk bytes, receipt dictionary, expense reference, and queue state. Identify which old resource is removed by the inspected code and which cleanup is not demonstrated.

Exercise NR05-B: build a validation table separating request size, declared file length, content type, filename extension, and actual bytes. State which checks currently exist and propose one bounded content-inspection experiment without claiming it already runs.

Exercise NR05-C: design a cancellation or copy-failure fixture using a disposable storage root. Explain request-token versus worker-token scope and why awaiting enqueue does not mean processing completed.
