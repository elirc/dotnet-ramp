# 08. Queue capacity and controlled scheduling

The [queue lab](labs/queue_lab.py) compiles a copy of [BackgroundTaskQueue](../../backend/Background/BackgroundTaskQueue.cs) in a disposable SDK project. Unlike a pure educational imitation, its queue behavior comes from the actual selected source. Its scope remains narrow: it does not execute the hosted worker, dependency-injection scopes, expense service, or payment simulation.

Run it with Python and a .NET 10 SDK. It has no package references and does not build the application project. The runner uses an owned temporary directory and compares the original queue file's hash before and after.

```powershell
python C:/Users/Owner/Desktop/.netramp/learn/extended-course/labs/queue_lab.py
```

## Make capacity observable

The lab constructs a queue with capacity one rather than the application's default capacity of one hundred. Capacity one makes the first occupied slot and the next waiting writer easy to reason about. It does not change the queue algorithm; it reduces the fixture to the smallest useful state.

The first delegate is enqueued into an empty queue. The returned ValueTask is expected to be completed successfully, and the lab awaits it. A second delegate is then enqueued while the slot is occupied. The lab converts that second ValueTask into a Task once and verifies that it is not completed before any dequeue occurs.

This observation tests backpressure directly. A delay-based test might sleep and infer that a writer is blocked because nothing happened yet. The capacity fixture instead creates a state in which the selected FullMode.Wait behavior has a concrete reason to remain pending. Scheduling noise is less likely to obscure the contract.

## Follow acceptance through a dequeue

The lab dequeues the first delegate and checks reference identity, not merely that some function was returned. That frees capacity for the second write. It then awaits the pending writer with a bounded watchdog and verifies successful completion. Finally it dequeues the second delegate and checks its identity.

The watchdog is not used to prove ordering through elapsed time. It prevents a broken lab or queue implementation from waiting indefinitely after the state transition that should permit progress. Distinguish a timeout used as a failure bound from a sleep used as an unreliable synchronization mechanism.

The delegates are never invoked. Their bodies are harmless placeholders, but the test is about queue acceptance and retrieval, not work completion. Returning the correct delegate does not prove a hosted service will execute it, create a scope, catch its exception, or honor a shutdown token.

## Cancellation belongs to the read in this interface

After emptying the queue, the lab cancels a token and attempts to dequeue. It expects OperationCanceledException. This establishes the selected empty-read cancellation behavior for the copied source. It does not establish cancellation of an already accepted job or cancellation of a pending writer.

EnqueueAsync has no cancellation-token parameter in this interface. A future proposal to cancel waiting producers would require an interface and caller change or another explicit mechanism. Do not claim that passing a request token to an unrelated earlier operation automatically cancels this wait.

The final baseline check passes null work and expects ArgumentNullException when the enqueue awaitable is observed. This is a separate input contract from capacity. Combining null work with a full queue would make it harder to see which boundary was responsible for the result, so the lab checks it after the queue has been drained.

## Seven assertions and one meaningful mutation

The baseline checks first acceptance, second-write pending state, first delegate identity, pending-write completion after capacity is freed, second delegate identity, canceled empty read, and null-work rejection. That is seven assertions over a specific sequence, not an exhaustive concurrency proof.

The mutation replaces FullMode.Wait with FullMode.DropWrite in a copied file. Both values are valid channel options, so the mutation should compile. The lab should then fail the assertion that the second write remains pending when the queue is full. This demonstrates that the fixture detects a semantic change in the handoff contract.

A dropped-write policy might be appropriate for some telemetry workloads, but it is not equivalent to waiting for expense-processing capacity. The lesson is to identify what the caller observes and what work can disappear under the proposed policy. Do not label every alternative option universally wrong; explain why it violates this declared queue contract.

The runner accepts a mutation failure only after a successful build and only when the intended semantic failure text appears. A compiler error or missing SDK is not a successful mutation experiment. That distinction prevents infrastructure failures from inflating test confidence.

## Connect the queue result to callers carefully

ReceiptService awaits enqueue, so a full queue can delay its return after file and metadata changes have already happened. StartReimbursement does not await the selected enqueue call, so the service may return without observing the pending acceptance. The queue lab establishes that a pending acceptance state exists; source inspection establishes how the callers handle the returned awaitable.

Together, these facts support a precise next experiment: use the real queue at capacity one, occupy it, invoke a selected service path with controlled dependencies, and observe whether the service returns before freeing capacity. Such a service experiment would execute additional code and require its own fixture. It has not been run merely because the queue lab passed.

Also consider queue lifetime. An in-memory bounded channel disappears when its owning process ends. Waiting for acceptance does not create durable storage. A reliability proposal should state whether accepted work must survive restart and, if so, how the work record and business state are committed together.

## Shutdown needs a separate contract

The hosted service receives a stopping token for dequeue and job execution. The queue itself does not expose a completion method in the selected interface. A full shutdown analysis would ask whether pending producers are released, whether accepted jobs are drained, and how unfinished work is reported.

Those questions cannot be answered by one canceled empty read. The baseline cancellation assertion is valuable precisely because its scope is explicit. A future shutdown lab should control a running job and queued jobs separately, trigger cancellation, and record each producer and consumer outcome without relying on arbitrary delays.

Choose an operational requirement before implementing that lab. A fast shutdown that abandons in-memory work and a graceful drain with a deadline are different contracts. Neither should be inferred solely from the presence of CancellationToken parameters.

## Independent exercises

Exercise NR08-A: draw the capacity-one timeline and label the point at which each enqueue is invoked, accepted, and later dequeued. Explain why the second writer's pending state is observable without executing either delegate.

Exercise NR08-B: predict the DropWrite mutation and identify the exact assertion that should fail after compilation. Explain why a seven-assertion pass does not prove worker execution or durable acceptance.

Exercise NR08-C: design a shutdown experiment with one running job, one queued job, and one waiting producer. Define whether your proposed contract drains, cancels, or records each item, and state which current interfaces would need additional behavior.
