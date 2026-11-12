# ForkForward.Mediator.Contracts

The contracts of [ForkForward.Mediator](https://www.nuget.org/packages/ForkForward.Mediator), a community fork of [MediatR](https://github.com/LuckyPennySoftware/MediatR): the code of MediatR.Contracts 2.0.1, which the original authors published under the Apache License 2.0. Independent project: not affiliated with, sponsored by, or endorsed by the original authors.

It contains only:

- `IRequest` (including generic variants)
- `INotification`
- `IStreamRequest`
- `Unit`

Reference it alone where your requests and notifications live in a separate assembly from their handlers, for example API contracts, gRPC contracts or Blazor. `ForkForward.Mediator` depends on it, so a project that references the mediator does not need it as well.

```csharp
using MediatR;

public record Ping(string Message) : IRequest<string>;
public record Pinged : INotification;
```

## Moving from MediatR.Contracts

Replace the `MediatR.Contracts` package reference with `ForkForward.Mediator.Contracts`. The assembly (`MediatR.Contracts.dll`), the namespace (`MediatR`) and the types are the same, so no code changes. Do not reference both packages in one project: they contain an assembly with the same name.

## Links

- Source: https://github.com/ForkForward/dotnet-mediator
- Changelog: https://github.com/ForkForward/dotnet-mediator/blob/main/CHANGELOG.md
- Licence: Apache License 2.0. Copyright Jimmy Bogard and MediatR contributors; modifications copyright 2026 ForkForward contributors. See NOTICE.md in the package.
