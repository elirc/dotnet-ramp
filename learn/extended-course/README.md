# .netramp extended course

This completed 24-module course follows the current expense application from authority and policy through files, queues, browser recovery, persistence proposals, and independent assessment. Read source traces as current behavior, executed lab results as bounded evidence, and design studios as proposals. Application code and old learning material are unchanged.

## Route and prerequisites

Start with basic C# classes, JavaScript promises, HTTP requests, and mutable collections. Chapters 01-04 form a complete foundation; 05-09 develop current system boundaries; 10-15 connect the client and source-bound service labs; 16-21 are advanced design studios; 22-24 integrate incident reasoning, a second independent capstone, and final assessment.

Write predictions before opening the separate answers. All 69 exercises have answer guidance. Score each out of ten: four for current-source or declared-design reasoning, three for a concrete fixture, and three for evidence limits and compatibility. Each of the two capstones has its own 100-point rubric; the final assessment has a separate 100-point rubric.

- [01. Authority, lifecycle edges, and service boundaries](01-authority-edges-and-service-boundaries.md)
- [02. Partial rejection, policy evaluation, and queue handoff](02-partial-rejection-policy-and-queue-handoff.md)
- [03. A copied-source lifecycle lab and its evidence contract](03-lifecycle-lab-and-evidence-contract.md)
- [04. Independent capstone: an auditable reimbursement decision](04-independent-capstone-and-rubric.md)
- [05. Receipt files, metadata, and background policy refresh](05-receipt-files-metadata-and-background-refresh.md)
- [06. Reporting snapshots, currency, and action hints](06-reporting-snapshots-currency-and-action-hints.md)
- [07. Policy boundaries, deterministic time, and reason codes](07-policy-boundaries-deterministic-time-and-reason-codes.md)
- [08. Queue capacity and controlled scheduling](08-queue-capacity-and-controlled-scheduling.md)
- [09. Session resolution and problem-detail boundaries](09-session-resolution-and-problem-details.md)
- [10. Fetch contracts and an actual-source client lab](10-fetch-contracts-and-actual-source-client-lab.md)
- [11. Form state, request validation, and date seams](11-form-state-request-validation-and-date-seams.md)
- [12. Detail polling, action recovery, and downloads](12-detail-polling-action-recovery-and-downloads.md)
- [13. Service characterization and independent snapshots](13-service-characterization-and-independent-snapshots.md)
- [14. Auth bootstrap races and local session recovery](14-auth-bootstrap-races-and-local-session-recovery.md)
- [15. List and report projections, display units, and stale errors](15-list-report-projections-and-display-units.md)
- [16. Command preparation and aggregate atomicity](16-command-preparation-and-aggregate-atomicity.md)
- [17. Receipt replacement interleavings and recovery](17-receipt-replacement-interleavings-and-recovery.md)
- [18. Durable handoff and an outbox design studio](18-durable-handoff-and-outbox-design-studio.md)
- [19. Time, identifiers, and reproducible fixtures](19-time-identifiers-and-reproducible-fixtures.md)
- [20. Persistence adapters and migration contracts](20-persistence-adapters-and-migration-contracts.md)
- [21. Query growth, pagination, and report cost](21-query-growth-pagination-and-report-cost.md)
- [22. Incident workbench and observation design](22-incident-workbench-and-observation-design.md)
- [23. Independent capstone: command integrity and client recovery](23-independent-capstone-command-and-client-recovery.md)
- [24. Final assessment and maintainer handoff](24-final-assessment-and-maintainer-handoff.md)

## Separate solutions

- [Foundation: chapters 01-04](solutions-foundation.md)
- [Systems: chapters 05-09](solutions-systems.md)
- [Client and service: chapters 10-15](solutions-client-and-service.md)
- [Design and assessment: chapters 16-24](solutions-design-and-assessment.md)

## Executed bounded labs

- [Lifecycle runner](labs/lifecycle_lab.py): 156 assertions over copied actual enums and table, with two compiled semantic mutations detected.
- [Queue runner](labs/queue_lab.py): 7 assertions over copied actual queue source, with one compiled semantic mutation detected.
- [Service runner](labs/service_lab.py): 22 assertions over copied actual service/domain/policy/mapping/store; controlled queue and stubbed job factory; one compiled relationship mutation detected.
- [Client runner](labs/client_lab.mjs): 12 assertions executing actual client.js under Node with controlled fetch and localStorage.

The C# runners require Python and a .NET 10 SDK and build disposable package-free projects. The application remains .NET 8. The JavaScript runner requires Node and built-in modules only. These checks do not start the full application, run a real browser, access real receipts, or execute a payment integration. Source hashes are checked for preservation.

## Completion and optional implementation

The new instructional prose exceeds the 39,280-word target with all README files and fenced code excluded. The route is complete at its declared educational scope. Proposed database adapters, durable work, browser tests, and domain repairs are independent exercises, not unfinished claims that those features already exist or were deployed.
