# Roadmap

Where ForkForward.Mediator is going, and the decisions that are still open. Open decisions are marked **Decision needed**; nothing depending on them is done until a maintainer has recorded the answer here.

## Phase 0: identity and housekeeping

- [x] Repository created as a GitHub fork of `LuckyPennySoftware/MediatR`, with its only branch and its tags at or before `v12.5.0` (commit `8a64581e`), the last release under the Apache License 2.0
- [x] Ruleset on `main` (2026-11-12): changes only through pull requests, no force pushes, no deletion, no bypass for administrators. GitHub's "Sync fork" is rejected with "Repository rule violations found"; checked against `main` itself
- [x] `origin guard` job in CI: fails when the history contains a commit made in the original repository after `v12.5.0`
- [ ] Add `origin guard` to the ruleset as a required status check, once CI has run for the first time
- [x] State clearly that this is a fork of MediatR, who wrote the original, and under what licence ([README.md](README.md), [NOTICE.md](NOTICE.md))
- [x] Own name and logo; remove things that applied only to the original project (its package feeds, badges and push scripts)
- [x] Rules for AI-assisted development ([AI_POLICY.md](AI_POLICY.md))
- [x] Issue forms, pull request template, code owners, label set
- [x] The solution builds and the 158 tests pass with a current .NET SDK (checked with 11.0.100 on Windows)
- [x] CI workflow for Windows and Ubuntu
- [ ] CI green on GitHub on both systems. This repository is a fork, and on a fork workflows do not run until someone enables them on the Actions tab
- [x] Repository description and topics `forkforward`, `dotnet`, `mediator`
- [ ] Repository settings: issues enabled, labels from `.github/labels.yml` applied
- [ ] Dependabot version updates switched on in the repository settings (Settings > Advanced Security > Dependabot). On a fork `dependabot.yml` does nothing until they are enabled there

## Phase 1: keep it building

- [x] Dependency review on pull requests: known vulnerabilities and licences of added or updated dependencies
- [ ] Make the CI build and Dependency review checks required on `main`, next to `origin guard`
- [ ] **Decision needed: which .NET versions the library targets.** Today `netstandard2.0` and `net6.0`, as in MediatR 12.5.0. .NET 6 is out of support and the SDK warns about it (`NETSDK1138`)
- [ ] Tests, benchmarks and samples on a supported .NET version (today the tests target `net8.0`, the benchmarks and one sample `net6.0`)
- [ ] Performance gate: compare speed and allocations with the last release on pull requests and before publishing, as `Benchmark.ps1` does in dotnet-mapper. The benchmarks in `test/MediatR.Benchmarks` are the starting point
- [ ] Fix the intermittent failure of `GenericRequestHandlerTests.ShouldThrowExceptionWhenTimeoutOccurs` (1 of 4 local runs on 2026-11-12). The cause is in the code inherited from 12.5.0: `ServiceRegistrar` keeps the generic-handler registration limits (`MaxTypesClosing`, `RegistrationTimeout` and two more) in static fields, so two `AddMediatR` calls running at the same time overwrite each other's limits, and xUnit runs test classes in parallel. It is a real defect for applications that build two containers concurrently with different limits, not only a test problem. Until it is fixed, a red CI run with only this test failing is rerun
- [ ] Work through the Dependabot updates
- [ ] Check the public advisories for MediatR up to 12.5.0; anything found is fixed independently, from the advisory and this code
- [ ] The stale-issue workflow inherited from the original (`triage-issues.yml`) closes issues after 74 days without activity: keep it, change it or remove it

## Phase 2: first release of `ForkForward.Mediator`

Nothing has been published. The package id `ForkForward.Mediator` is free on nuget.org (checked 2026-11-12).

1. **Decision needed: namespace and assembly name.** Today the assembly is `MediatR.dll` and the namespace is `MediatR`, so the package is a drop-in replacement but cannot be told apart from the original by its assembly, and both cannot be referenced together. dotnet-mapper took its own names (`ForkForward.Mapper`) at its first stable release, so that moving a project is a package reference and a `using` directive. The same choice here would be `ForkForward.Mediator`.
2. **Decision needed: strong-name key.** Today the assembly is signed with the original `MediatR.snk`, which is in the repository. An own name goes with an own key.
3. **Decision needed: version numbers.** Provisionally the versions continue the 12.x line above the original: MinVer builds `12.6.0-preview.0.<n>` for untagged commits, and a tag `v12.6.0` would build `12.6.0`. The line signals compatibility with MediatR 12.
4. **Decision needed: the contracts package.** The library depends on the NuGet package `MediatR.Contracts`, range `[2.0.1, 3.0.0)`. Version 2.0.1 was published by the original authors on 2023-02-22 under the Apache License 2.0, and it is what NuGet resolves today. The `MediatR.Contracts` project in this repository is the same code but is no longer packed, because that package id belongs to the original authors. The choices: keep depending on the original 2.0.1 (types stay identical to the ones the official MediatR uses) and pin it to exactly `[2.0.1]`, so that a later 2.x under another licence can never be resolved; or publish `ForkForward.Mediator.Contracts` from this repository.
5. [ ] Publishing from the release workflow. `release.yml` is ready and publishes through NuGet Trusted Publishing, with no stored API key. It needs three things outside this repository: a Trusted Publishing policy on nuget.org, owned by the `ForkForward` organization, for `ForkForward/dotnet-mediator`, workflow file `release.yml`, environment `release`; the `NUGET_USER` secret with the nuget.org user name of the person who created that policy (not the organization); and, recommended, required reviewers on the `release` environment. Nothing is published until a version tag `v*.*.*` is pushed.
6. [x] NuGet-specific readme (`src/MediatR/PACKAGE.md`) and `CHANGELOG.md`, linked from the package as release notes.
7. [ ] A guide "Moving from MediatR 12", once decisions 1 and 4 are taken.

## Phase 3: beyond the first release

- [ ] Documentation site at forkforward.dev/dotnet-mediator. The original documentation is a GitHub wiki, which is not part of the forked repository and now describes the newer versions; the documentation here has to be written from this code.
- [ ] List the project on [forkforward.dev](https://forkforward.dev) and in the organisation profile.
- [ ] Compare the public API with the previous release on every pull request (for example with `Microsoft.DotNet.ApiCompat.Tool`).
