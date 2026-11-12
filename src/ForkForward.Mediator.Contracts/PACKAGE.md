# ForkForward.Mediator.Contracts

The contracts of [ForkForward.Mediator](https://www.nuget.org/packages/ForkForward.Mediator), a community fork of [MediatR](https://github.com/LuckyPennySoftware/MediatR): the code of MediatR.Contracts 2.0.1, which the original authors published under the Apache License 2.0, with its own assembly name, namespace and signing key. Independent project: not affiliated with, sponsored by, or endorsed by the original authors.

It contains only:

- `IRequest` (including generic variants)
- `INotification`
- `IStreamRequest`
- `Unit`

Reference it alone where your requests and notifications live in a separate assembly from their handlers, for example API contracts, gRPC contracts or Blazor. `ForkForward.Mediator` depends on it in exactly its own version, so a project that references the mediator does not need it as well. Both packages always have the same version number.

```csharp
using ForkForward.Mediator;

public record Ping(string Message) : IRequest<string>;
public record Pinged : INotification;
```

## Moving from MediatR.Contracts

Replace the `MediatR.Contracts` package reference with `ForkForward.Mediator.Contracts`, and `using MediatR` with `using ForkForward.Mediator`. The types and their members are the same. Full guide: [Moving from MediatR 12](https://github.com/ForkForward/dotnet-mediator/blob/main/docs/Moving-from-MediatR-12.md).

Version 2.6.0 of this package predates the rename: it contains `MediatR.Contracts.dll` with the namespace `MediatR`. Use 12.6.0-preview.2 or later.

## Links

- Source: https://github.com/ForkForward/dotnet-mediator
- Changelog: https://github.com/ForkForward/dotnet-mediator/blob/main/CHANGELOG.md
- Licence: Apache License 2.0. Copyright Jimmy Bogard and MediatR contributors; modifications copyright 2026 ForkForward contributors. See NOTICE.md in the package.
