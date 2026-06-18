# Repository Guidance

## DevContext MCP

Use the `devcontext` MCP server for questions about indexed internal NuGet
packages, public .NET symbols, implementation examples, and company
documentation. Prefer it over model memory or general web search for those
subjects.

Verify that the `devcontext` MCP server is available before making plan and code
changes. If it is unavailable, stop implementation work and inform the user.

Follow this workflow:

1. Call `resolve_library` with the package, client, or implementation concept.
2. For NuGet libraries, call `list_versions` and select a version compatible
   with the project. Prefer the project's referenced version when available.
3. Call `query_docs` for implementation guidance and examples, or `get_symbol`
   for a specific public type or member.
4. Preserve citation URIs and mention important warnings or insufficient
   evidence in the answer.

For company documentation, resolve or query `docs:company-docs`. Do not call
`list_versions` or `get_symbol` for company documentation.

Do not invent APIs when DevContext returns `not_found`,
`insufficient_evidence`, or `not_ready`. Inspect the local repository for
additional evidence and clearly state any remaining uncertainty.

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
