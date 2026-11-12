# Contributing to ForkForward.Mediator

ForkForward.Mediator is a community fork of MediatR. It is maintained with AI coding tools, and reviewed and released by people. Read [AI_POLICY.md](AI_POLICY.md) first; it is short and it is the part that differs from other projects.

## What helps most

- **A bug report with a reproduction.** The request or notification type, its handlers, how they are registered, the call that fails, and what you expected. A failing test is ideal.
- **A compatibility report.** If something that worked with MediatR 12.5.0 behaves differently here, that is a bug in this fork.
- **A report that the build or the tests fail** on a current .NET SDK or operating system.

Report bugs in this fork here, in [ForkForward/dotnet-mediator](https://github.com/ForkForward/dotnet-mediator/issues). The original MediatR maintainers are not responsible for this code, so please do not send them issues about it.

## Pull requests

- Pull requests go to the `main` branch of **this** repository. GitHub proposes the original repository as the target because this one is a fork of it; change the base repository to `ForkForward/dotnet-mediator` before you open the pull request.
- The change is produced with an AI coding tool, and the pull request says which one and what you checked yourself. The template asks for this.
- Every bug fix and feature comes with a test.
- The public API stays compatible with MediatR 12.5.0, under the `ForkForward.Mediator` namespace. A change to a public type or member is explained in the pull request.
- Discuss larger changes in an issue before you start.

## Where code may come from

Do not copy anything from MediatR 13.0.0 or later, or from commits made to the `LuckyPennySoftware/MediatR` repository after this fork was taken (commit `8a64581e`, tag `v12.5.0`). That code is under a different licence and cannot be used here, in any form: no code, tests, documentation, or ports of them. A fix that also exists there has to be written independently. For the same reason, do not use GitHub's "Sync fork" button and do not merge from the original repository. The full rule is in [AI_POLICY.md](AI_POLICY.md#4-where-code-may-come-from).

By opening a pull request you confirm that you have the right to submit the change and that it can be released under the Apache License 2.0.

## Building

The project builds and is tested on Windows and on Linux; CI runs both. You need a current .NET SDK (10 or newer) and the .NET 8 runtime, which the tests run on. The solution file is `ForkForward.Mediator.sln`. The library targets `netstandard2.0` and `net6.0`.

```powershell
./Build.ps1
```

This cleans and builds the solution, runs all tests and packs `ForkForward.Mediator` and `ForkForward.Mediator.Contracts` into `artifacts/`.

To run only the tests:

```powershell
dotnet test test/ForkForward.Mediator.Tests
```

The SDK prints warning `NETSDK1138` for the projects that target `net6.0`. It is known; the target frameworks are an open decision in [ROADMAP.md](ROADMAP.md).

## Performance

The library must not get slower, or allocate more memory, than the last release. `Benchmark.ps1` checks it:

```powershell
./Benchmark.ps1
```

It builds the same benchmarks twice, against the library in this repository and against the last published package, measures both on your machine in alternating rounds, and prints a comparison. It takes about eight minutes. It fails when a call benchmark is more than 10 % slower than the baseline, or when any benchmark allocates more memory.

To compare with the code this fork started from:

```powershell
./Benchmark.ps1 -BaselinePackage MediatR -BaselineVersion 12.5.0 -BaselineNamespace MediatR
```

CI runs the same comparison on every pull request that changes the library, its dependencies or the benchmarks, and once more before a release is published. A few things to know about the result:

- **Allocations are exact.** They do not depend on how busy the machine is, so an increase is always a real one.
- **Timings are noisy on shared machines.** The fastest iteration of each side is compared, because other work on the machine only ever adds time. The benchmarks in `ReferenceBenchmarks` do not use the library, so both sides must measure the same there. When they do not, the run is reported as inconclusive instead of as a regression: run the job again.
- **CI measures on two runners and believes a slowdown only when both see it.** The two sides are two separately compiled assemblies of the same code, and on some machines the JIT lays one of them out less favourably; a single runner then reports one benchmark 10 % slower that another runner does not see at all. A slowdown on one runner only is shown as "not confirmed" and does not fail the check. More allocations on either runner do. Locally, `./Benchmark.ps1 -MergeVerdicts a/verdict.json, b/verdict.json` judges two runs the same way.
- **Startup benchmarks are checked for allocations only.** The time to register the handlers and build a container varies too much between runs to be a gate; it is shown for information.

New benchmarks go into `test/ForkForward.Mediator.Benchmarks`. The same sources are compiled against the baseline package, so a benchmark can only use API that the last release already has, and they do not name the library's namespace: each project supplies it. `Library.cs` is the only file that names the registration method.

After a release, raise `BaselineVersion` in `test/ForkForward.Mediator.Benchmarks.Baseline/ForkForward.Mediator.Benchmarks.Baseline.csproj` to the version just released.

## Commit messages

A short summary line, then what changed and why. Reference the issue number. Commits written by an AI tool carry a `Co-Authored-By` trailer naming the tool.
