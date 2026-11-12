# How this project is developed

ForkForward keeps open-source libraries alive after their original line of development stops being open. The maintenance work is done with AI coding tools and automation. This document says what that means in practice and who is responsible for what.

## 1. AI tools write the changes

Every change to this repository is produced with an AI coding tool or by automation such as Dependabot.

If you want something fixed, the most useful things you can give are a clear issue, a minimal reproduction, or a failing test. Pull requests are welcome when the change in them was produced the same way, and say so (see section 3).

## 2. People are responsible

AI tools do not merge, release or decide.

- A named maintainer reviews every change before it is merged. The reviewers are listed in [.github/CODEOWNERS](.github/CODEOWNERS).
- A change is merged only when the full test suite passes in CI.
- A maintainer decides what goes into a release and publishes it.
- The maintainer who merges a change is accountable for it, however it was written.

## 3. Every change says how it was made

Each pull request states:

- which AI tool and model produced the change,
- what the person submitting it checked by hand,
- which tests cover it.

Commits written by an AI tool carry a `Co-Authored-By` trailer naming the tool.

## 4. Where code may come from

This is the rule that protects the project and its users. It has no exceptions.

- The baseline is the Apache-2.0-licensed MediatR code at commit `8a64581e`, tag `v12.5.0` (see [NOTICE.md](NOTICE.md)).
- **Nothing is copied from MediatR 13.0.0 or later, or from any commit made to the `LuckyPennySoftware/MediatR` repository after that one.** Those are under a different licence. This covers code, tests, documentation and commit messages.
- Do not give that code to an AI tool as context, and do not ask a tool to reproduce or port it.
- **Never use GitHub's "Sync fork" button, and never fetch or merge from `LuckyPennySoftware/MediatR`.** This repository is a GitHub fork of it, so GitHub offers to bring in its newer commits. Those commits are the differently licensed code. Two things enforce this: a ruleset on `main` accepts changes only through pull requests, with no exceptions for administrators, so "Sync fork" is rejected; and the `origin guard` job in CI fails a pull request whose history contains a commit made in the original repository after `v12.5.0`.
- If the official MediatR fixes a bug that also exists here, the fix in this fork is written independently, starting from the bug report or a reproduction.
- Third-party code may be added only under a licence compatible with the Apache License 2.0, with its notice kept.

If you are not sure whether something is allowed, ask in an issue before you open the pull request.

## 5. Compatibility comes first

People use this fork so that existing code keeps working.

- The public API stays compatible with MediatR 12.5.0, under the `ForkForward.Mediator` namespace: the same types and members, except for the two names that carried the old name (`AddMediator`, `MediatorServiceConfiguration`), so that moving a project is a package reference, a `using` directive and those two names. A pull request that changes a public type or member says so, and needs an explicit decision by a maintainer.
- The namespace, the assembly names and the signing key were changed to the fork's own in 12.6.0-preview.2, by a decision recorded in [ROADMAP.md](ROADMAP.md). They, and type and member names, are not changed again without one.
- Behaviour changes need a test that shows the old and the new behaviour.
- Speed is part of compatibility. A change must not make the library slower, or make it allocate more memory, than the last release. An automatic comparison in CI, like the one in [dotnet-mapper](https://github.com/ForkForward/dotnet-mapper), is planned in the roadmap; until it exists, a pull request that touches the library says how this was checked.

## 6. Credit

The original authors' copyright and licence notices are never removed or edited. Documentation that describes the project says that it is a fork of MediatR and links to the original.
