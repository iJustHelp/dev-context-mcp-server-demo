# Repository Guidance

## dev_context MCP Server

DevContext provides indexed internal NuGet package documentation, public .NET symbols, implementation examples, and company documentation.

Use this workflow:

1. Call `resolve_library` first with the package name, client name, type name, or implementation concept.
2. For NuGet libraries, call `list_versions` and select a version compatible with the current project. Prefer the project's referenced version when known.
3. Use `query_docs` for implementation guidance, examples, warnings, and usage patterns.
4. Use `get_symbol` only for a specific public type or member.
5. Preserve citation URIs and mention important warnings, missing documentation, or insufficient evidence.

For company documentation, resolve or query `docs:company-docs`. Do not call `list_versions` or `get_symbol` for company documentation.

Do not invent APIs when results are `not_found`, `insufficient_evidence`, or `not_ready`. Clearly state uncertainty and recommend inspecting the local repository for additional evidence.


## Unit Tests

For classes that use dependency injection:

1. Query `docs:company-docs` through DevContext for the current unit test
   template before creating or changing tests.
2. Follow that template and use `Moq` for injected collaborators, including
   services, repositories, clients, clocks, and other dependencies.
3. Do not create hand-written fake, stub, or test implementations of injected
   interfaces when Moq can express the required behavior.
4. Verify interactions that are part of the behavior, such as call counts,
   arguments, cancellation tokens, and the absence of calls.

Use real implementations only when the test is intentionally an integration
test, such as exercising SQLite persistence or ASP.NET Core hosting.
