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
- [x] CI green on GitHub on both systems (pull request #1, 2026-11-12)
- [x] Repository description and topics `forkforward`, `dotnet`, `mediator`
- [ ] Repository settings: issues enabled, labels from `.github/labels.yml` applied
- [ ] Dependabot version updates switched on in the repository settings (Settings > Advanced Security > Dependabot). On a fork `dependabot.yml` does nothing until they are enabled there

## Phase 1: keep it building

- [x] Dependency review on pull requests: known vulnerabilities and licences of added or updated dependencies
- [ ] Make the CI build and Dependency review checks required on `main`, next to `origin guard`
- [ ] **Decision needed: which .NET versions the library targets.** Today `netstandard2.0` and `net6.0`, as in MediatR 12.5.0. .NET 6 is out of support and the SDK warns about it (`NETSDK1138`)
- [ ] Tests, benchmarks and samples on a supported .NET version (today the tests target `net8.0`, the benchmarks and one sample `net6.0`)
- [ ] Performance gate: compare speed and allocations with the last release on pull requests and before publishing, as `Benchmark.ps1` does in dotnet-mapper. The benchmarks in `test/ForkForward.Mediator.Benchmarks` are the starting point
- [ ] Fix the intermittent failure of `GenericRequestHandlerTests.ShouldThrowExceptionWhenTimeoutOccurs` (1 of 4 local runs on 2026-11-12). The cause is in the code inherited from 12.5.0: `ServiceRegistrar` keeps the generic-handler registration limits (`MaxTypesClosing`, `RegistrationTimeout` and two more) in static fields, so two `AddMediator` calls running at the same time overwrite each other's limits, and xUnit runs test classes in parallel. It is a real defect for applications that build two containers concurrently with different limits, not only a test problem. Until it is fixed, a red CI run with only this test failing is rerun
- [ ] Work through the Dependabot updates
- [ ] Check the public advisories for MediatR up to 12.5.0; anything found is fixed independently, from the advisory and this code
- [ ] The stale-issue workflow inherited from the original (`triage-issues.yml`) closes issues after 74 days without activity: keep it, change it or remove it

## Phase 2: first release of `ForkForward.Mediator`

The first preview was published on 2026-11-12: tag `v12.6.0-preview.1`, [`ForkForward.Mediator` 12.6.0-preview.1](https://www.nuget.org/packages/ForkForward.Mediator/12.6.0-preview.1) and [`ForkForward.Mediator.Contracts` 2.6.0](https://www.nuget.org/packages/ForkForward.Mediator.Contracts/2.6.0) on nuget.org, [GitHub release](https://github.com/ForkForward/dotnet-mediator/releases/tag/v12.6.0-preview.1) created by the workflow. It was published before decisions 1 to 3 below were taken, by the maintainer's choice, as dotnet-mapper published its first previews: the assemblies are `MediatR.dll` and `MediatR.Contracts.dll`, signed with the original key.

1. **Decided (maintainer, 2026-11-12): own assembly names and namespace, and no name of the original project in the API.** The assemblies are `ForkForward.Mediator.dll` and `ForkForward.Mediator.Contracts.dll` and the namespaces start with `ForkForward.Mediator`. The identifiers that carried the old name are renamed without `[Obsolete]` copies: `AddMediator`, `MediatorServiceConfiguration`, `ServiceRegistrar.AddMediatorClasses` and `AddMediatorClassesWithTimeout`. Projects, folders and the solution are renamed too. The fork cannot be confused with the original and can be referenced next to it; moving a project is a package reference, a `using` directive and those names ([Moving from MediatR 12](docs/Moving-from-MediatR-12.md)). Libraries compiled against `MediatR.dll` have to be recompiled. The exported types and members were compared with the published `12.6.0-preview.1`, which is the 12.5.0 code: 44 types and 188 members, no difference after the renames.
2. **Decided (maintainer, 2026-11-12): own strong-name key.** `ForkForward.Mediator.snk`, public key token `fe080643cbba1f62`, for both assemblies. The original key is no longer in the repository.
3. **Decided (maintainer, 2026-11-12): version numbers continue from 12, one version for both packages.** MinVer takes it from the `v*.*.*` tags for the mediator and the contracts alike; `12.7.0` is the first stable release. The 12.6 line is skipped: its only version is the preview that went out with the original names. The line signals compatibility with MediatR 12. A preview therefore never publishes a stable version of either package.
4. **Decided (maintainer, 2026-11-12): the fork publishes its own contracts package and pins it.** `src/ForkForward.Mediator.Contracts` is packed as `ForkForward.Mediator.Contracts` with the version of the mediator. `ForkForward.Mediator` references the project and its package depends on exactly that version, for example `[12.7.0]`; it does not depend on the original `MediatR.Contracts` package, so no later version of that package can ever be resolved. The code and the public types are those of `MediatR.Contracts` 2.0.1. `ForkForward.Mediator.Contracts` 2.6.0, published with the first preview under a version of its own and with the original assembly name, is superseded.
5. [x] Publishing from the release workflow: working since `12.6.0-preview.1`. `release.yml` publishes through NuGet Trusted Publishing, with no stored API key, and only for a version tag `v*.*.*`. Before it publishes it checks that the history holds nothing committed in the original repository after `v12.5.0`, that exactly two packages were built, `ForkForward.Mediator` and `ForkForward.Mediator.Contracts`, both with the version of the tag, that `LICENSE` and `NOTICE.md` are in both, and that the first depends on exactly the version of the second and on no package of the original project. Run by hand it is a dry run that publishes nothing. What it needed:
   - [x] The environment `release` exists in this repository. Recommended and not done: required reviewers on it, so that the publish job waits for a maintainer's approval.
   - [x] Both packages were installed from a local feed into a new .NET 8 application with a separate contracts-only library, and used there (`AddMediator`, `Send`, `Publish`); NuGet restored no package of the original project.
   - [x] On nuget.org, a Trusted Publishing policy owned by the `ForkForward` organization: repository owner `ForkForward`, repository `dotnet-mediator`, workflow file `release.yml`, and no environment, so that it covers any environment the workflow uses.
   - [x] The repository secret `NUGET_USER` is set (by the maintainer, 2026-11-12). It has to be the nuget.org user name of the person who creates that policy, not the organization and not an e-mail address.
   - [x] The first preview was published before decisions 1 to 3 were taken (maintainer, 2026-11-12). It and `ForkForward.Mediator.Contracts` 2.6.0 carry the original assembly names.
   - [ ] On nuget.org, unlist `ForkForward.Mediator` 12.6.0-preview.1 and `ForkForward.Mediator.Contracts` 2.6.0 once 12.7.0 is out, so that nobody picks the versions with the original names (only an owner of the packages can do this; a published version cannot be deleted or replaced).
   - [x] First deploy: dry run of the release workflow on `main`, then the tag `v12.6.0-preview.1`. The published package was then installed from nuget.org into a new .NET 8 application and used there.

   Release steps for the next version: merge what goes in; start the Release workflow by hand on `main` for a dry run; push the tag `v<version>` on `main`. Both packages are published with that version.
6. [x] NuGet-specific readmes (`src/ForkForward.Mediator/PACKAGE.md`, `src/ForkForward.Mediator.Contracts/PACKAGE.md`) and `CHANGELOG.md`, linked from the package as release notes.
7. [x] The guide [Moving from MediatR 12](docs/Moving-from-MediatR-12.md).
8. [ ] `12.7.0`: the first stable release and the first version with the fork's own names. Its section is in `CHANGELOG.md`; it is published by the tag `v12.7.0` on `main`, after a dry run of the release workflow.

## Phase 3: beyond the first release

- [ ] Documentation site at forkforward.dev/dotnet-mediator. The original documentation is a GitHub wiki, which is not part of the forked repository and now describes the newer versions; the documentation here has to be written from this code.
- [ ] List the project on [forkforward.dev](https://forkforward.dev) and in the organisation profile.
- [ ] Compare the public API with the previous release on every pull request (for example with `Microsoft.DotNet.ApiCompat.Tool`).
