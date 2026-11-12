# NOTICE

## ForkForward.Mediator

ForkForward.Mediator is a fork of MediatR. This file records where the code comes from, who owns what, and what the fork has changed. It ships in the source repository and inside the NuGet package.

## Original work

- **Project:** MediatR, a simple mediator implementation in .NET
- **Authors:** Jimmy Bogard and the MediatR contributors (the full list is in the git history of this repository)
- **Copyright:** Copyright Jimmy Bogard
- **Licence:** Apache License, Version 2.0. The original licence text is kept unchanged in [LICENSE](LICENSE).
- **Source:** <https://github.com/LuckyPennySoftware/MediatR> (formerly `jbogard/MediatR`)
- **Fork point:** commit `8a64581e2570e7410c5f44835d9ea112beccbe6b` (2025-03-24), which is tag `v12.5.0`
- **Official project today:** <https://github.com/LuckyPennySoftware/MediatR>

The complete commit history of the original project up to the fork point is preserved in this repository. The original project had no NOTICE file at the fork point.

## This fork

- **Project:** ForkForward.Mediator, <https://github.com/ForkForward/dotnet-mediator>
- **Copyright in the modifications:** Copyright 2026 ForkForward contributors
- **Licence of the modifications:** Apache License, Version 2.0, the same terms as the original work

Changes in this fork are written with AI coding tools and automation and are reviewed by people. See [AI_POLICY.md](AI_POLICY.md).

## No affiliation

ForkForward is an independent project. It is not affiliated with, sponsored by, or endorsed by Jimmy Bogard, Lucky Penny Software or the MediatR project.

"MediatR" is the name of the original project and belongs to its owners. This fork uses the name only to identify the origin of the code and to describe compatibility. Since version 12.7.0 the assemblies (`ForkForward.Mediator.dll`, `ForkForward.Mediator.Contracts.dll`), the namespaces and the signing key are the fork's own, and no public type or member carries the old name: `AddMediatR` became `AddMediator` and `MediatRServiceConfiguration` became `MediatorServiceConfiguration`. The fork is distributed under its own name, `ForkForward.Mediator`, and with its own logo.

## Code that must not enter this fork

Nothing may be copied into this fork from MediatR 13.0.0 or later, or from any commit of the `LuckyPennySoftware/MediatR` repository made after the fork point. That code is under a different licence (Reciprocal Public License 1.5 or a commercial licence).

This repository is a GitHub fork of `LuckyPennySoftware/MediatR`, so GitHub offers a "Sync fork" button. It must not be used: it would merge the later, differently licensed code. A ruleset on the `main` branch rejects it.

## What the fork has changed

The authoritative list is the git history: `git log v12.5.0..HEAD`. As the Apache License 2.0 asks, files changed by the fork are identified there, commit by commit.

Summary by area:

- **Identity:** packages renamed to `ForkForward.Mediator` and `ForkForward.Mediator.Contracts`; assemblies, namespaces, projects and folders renamed from `MediatR` to `ForkForward.Mediator`; the identifiers that carried the old name renamed (`AddMediator`, `MediatorServiceConfiguration`, `AddMediatorClasses`, `AddMediatorClassesWithTimeout`); the original signing key replaced by the fork's own; new logo, and the original logo files removed; README and package readmes rewritten to state the origin of the code.
- **Removed things that do not apply to the fork:** the original package feeds and the scripts that pushed to them, the build badges of the original project, the DevSkim workflow.
- **Packaging:** `LICENSE` and this file are packed into both NuGet packages; versions continue above 12.5.0; the contracts are packed as `ForkForward.Mediator.Contracts`, because the package name `MediatR.Contracts` belongs to the original authors; both packages have one version and the mediator depends on exactly that version of the contracts instead of on the original package.
- **Build:** continuous integration runs on Windows and on Ubuntu; dependency changes are reviewed on pull requests; speed and allocations are compared with the last release on pull requests and before publishing; the benchmarks are rewritten for that comparison, and the dotTrace helper that came with them is removed; Dependabot configuration for dependency updates.
- **Project rules:** AI development policy, roadmap, contribution guide, issue forms and pull request template.

Apart from those renames no public API of the library or of the contracts has been changed: the exported types and members were compared with the 12.5.0 code, and after applying the renames there is no difference. Every source file is touched by the namespace rename; none has any other change.
