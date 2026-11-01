# Gocgun.Mediator

> **Beta.** This package is in beta: the public API may change between releases.

In-process request dispatch. MediatR-style types, not source-compatible with MediatR: methods are `SendAsync` and `HandleAsync` and return `ValueTask`.

- `IRequest<TResponse>`, `IRequestHandler<TRequest, TResponse>`, `IPipelineBehavior<TRequest, TResponse>`, `ISender`.
- `AddMediator` scans the assemblies you pass and registers each handler as scoped. `HandlerRegistryValidator` then requires exactly one handler for every `IRequest<>` in those assemblies. A request whose handler lives in an assembly you did not pass fails that check. Call `AddMediator` once with every assembly, or pass `validateHandlerRegistry: false` and call `HandlerRegistryValidator.Validate` after the last registration.
- `AddOpenBehavior` registers an open generic behavior. Registration order is execution order.
- `Sender` is internal. Callers use `ISender`.
- No notifications, no streaming, no benchmark project, no reference to `Gocgun.Results`.

Tests are unit tests. Dispatch is in memory, so an integration project would not exercise a different seam.

License: AGPL-3.0-only (`LICENSE`). Copyright holder exception: `LICENSE-EXCEPTION`.

## Publishing to nuget.org

Run `scripts/publish.sh` (or `scripts/publish.ps1` on Windows). It dispatches the manual `release.yml` workflow of each dependency first, in dependency order (Gocgun.Mediator), then of this package, waiting for each run and stopping on the first failure. Requires the GitHub CLI signed in with `gh auth login`; the `nuget` environment and the nuget.org Trusted Publishing policy must exist in each repository.
