# 02 — Stack & Language Mastery

Every concept here points to a real line in this repo. The goal is not "learn C#/React" in
the abstract, but to understand the exact features this code relies on — and the failure
modes that bite when you don't.

| Page | Teaches |
| --- | --- |
| [01-dotnet-runtime-model](01-dotnet-runtime-model.md) | CLR, GC, async/await, Task vs ValueTask, cancellation, IDisposable |
| [02-framework-mental-models](02-framework-mental-models.md) | ASP.NET Core pipeline + DI lifetimes; React render model |
| [03-type-system-and-contracts](03-type-system-and-contracts.md) | C# generics, nullable refs, records, pattern matching, LINQ deferral |
| [04-tooling-and-build-system](04-tooling-and-build-system.md) | SDK, MSBuild, implicit usings, NuGet, Vite |

Concept → why it matters → real code → failure mode → drill, throughout.
