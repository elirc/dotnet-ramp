# 03. A copied-source lifecycle lab and its evidence contract

The [lab runner](labs/lifecycle_lab.py) creates a temporary SDK project, copies the application's enum and lifecycle-table files, and compiles those copies with a small independent oracle. It does not edit the application, run the HTTP host, or exercise the expense service. That small scope is deliberate: it lets you test every combination in the finite lifecycle table without confusing table correctness with workflow atomicity.

Run the following command from any working directory with Python and a .NET 10 SDK available. The runner resolves source locations from its own file, so the command does not depend on your current directory. It uses the SDK to build a package-free temporary project and removes the temporary directory afterward.

```powershell
python C:/Users/Owner/Desktop/.netramp/learn/extended-course/labs/lifecycle_lab.py
```

The application itself targets .NET 8. The lab's disposable project targets .NET 10 because it uses the installed SDK for these two plain source files. A successful lab build is not a compatibility claim about the full application, its deployment environment, or every framework feature. If the required SDK is absent, record that prerequisite failure instead of calling the lifecycle policy broken.

## Count the oracle before running it

There are six defined expense statuses and three defined user roles. Testing every from-state, to-state, and role yields one hundred eight human-transition assertions. Testing every from-state and to-state yields thirty-six system-transition assertions. Six assertions compare NextStates with the independently declared edge sets, and six compare terminal classification. The baseline therefore expects one hundred fifty-six assertions.

The expected human edges are written explicitly in the lab, rather than derived by reflecting into the application's private transition dictionary. Deriving expected values from the same implementation would make the comparison circular. The oracle separately adds the system completion edge to the graph and expects the current broad system predicate to accept every legal graph edge.

An independent oracle can still contain mistakes. Review the expected set against a manually written graph before trusting its total. In particular, do not equate the empty human-role array with the complete set of edges accepted by CanSystemTransition. The current helper accepts legal edges regardless of the array. That distinction is exactly what one of the mutation cases probes.

## Read a baseline pass narrowly

A baseline pass establishes that the copied current source agrees with this declared finite oracle for the enumerated values. It also checks next-state sets and terminal classification. It does not establish ownership rules, manager relationships, policy severity, receipt processing, or persistence behavior because those components are not compiled into this lab.

The runner hashes both original source files before and after the cases. Equal hashes provide evidence that this lab did not change those files during its execution. They are not a complete filesystem audit and do not prove no other process touched unrelated files. Keep the preservation statement as precise as the measurement.

The table checks compare sets for NextStates because this lesson asks which destinations exist, not their enumeration order. If a user interface relies on order, that would be a separate contract requiring an ordered assertion and source review. Avoid accidentally broadening a set-equality test into an order guarantee.

## Why use semantic mutations

The first mutation changes the role allowed to approve a submitted expense from Manager to Finance in the copied source. It should compile successfully because both roles are valid enum members and the dictionary shape remains legal. The oracle should then fail a human transition assertion. This demonstrates that the test notices a meaningful authorization-policy change rather than merely catching syntax errors.

The second mutation narrows CanSystemTransition to edges whose role array is empty. That proposal may sound attractive if you read only the system-only comment on one edge, but it differs from the actual current helper contract. The copied mutant should compile and then fail a system-edge assertion, beginning with a legal nonempty-role edge under the enumeration order.

The runner requires a successful build for each mutation before accepting its failing execution as useful evidence. A missing SDK, changed source anchor, or compiler error is an infrastructure or adaptation problem, not a killed semantic mutation. Do not count any nonzero process exit as proof that the oracle detects policy defects.

## Interpret failures in layers

If the source files cannot be found, first verify the runner still lives under the expected course directory and the project structure has not moved. If a mutation anchor does not occur exactly once, reread the current file and update the educational mutation deliberately. An anchor mismatch may mean the application changed legitimately; it does not automatically mean the course or application is wrong.

If compilation fails, inspect the build output before touching the oracle. The copied files should compile with implicit framework usings enabled. A future source dependency could make this isolated compilation insufficient. In that case, revise the lab's scope or include a justified dependency rather than replacing real source with an unnoticed imitation.

If baseline execution fails after compilation, compare the reported transition with the declared oracle and current requirements. The disagreement might expose a source change, an incorrect expectation, or a deliberate new policy. Preserve the failing evidence and explain the decision. Do not change expected values solely to obtain a green result.

## Extend the evidence without overclaiming

A next service-level lab could instantiate DataStore, PolicyEngine, ExpenseService, and a controlled queue double. It would need fixture users and relationships, not just enum values. It could characterize the rejection-reason mutation and submission policy refresh with deep snapshots. That lab is a proposed extension; the current runner does not execute it.

A queue-handoff lab would need a controllable ValueTask whose completion can be delayed. It should distinguish invocation from successful acceptance and record whether the service returns while the awaitable remains pending. The table runner cannot answer this because it has no queue dependency. Keeping these proposed experiments separate makes the existing evidence more useful, not less valuable.

For a full request test, add authentication, request binding, response mapping, and host configuration to the evidence boundary. For a durable reimbursement guarantee, add persistence and recovery. These are additional contracts rather than hidden properties of the lifecycle table.

## Independent exercises

Exercise NR03-A: reproduce the one hundred fifty-six assertion count and explain why the expected edges must not be read from the implementation dictionary. Identify one important service behavior outside this count.

Exercise NR03-B: before running, predict the type of failure for each semantic mutation. Explain why a compiler failure would not satisfy the mutation experiment and why the narrower system helper is a changed contract rather than a harmless refactor.

Exercise NR03-C: design the next service fixture for an Approved expense rejected by its valid manager. List the exact snapshot fields, setup relationships, and evidence needed to distinguish unchanged status from unchanged object state. Keep it as a proposal until you execute a real service test.
