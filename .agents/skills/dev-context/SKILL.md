---
name: dev-context
description: Research internal NuGet packages, unfamiliar .NET APIs, public symbols, implementation examples, and company engineering standards through the dev_context MCP server. Use before implementing or reviewing code that depends on internal packages or uncertain .NET APIs, when company documentation or architecture guidance is needed, and whenever creating or changing unit tests for dependency-injected classes. Do not use for unrelated repository work whose APIs and standards are already established locally.
---

# DevContext

Use the live `dev_context` MCP server as the source of truth. Inspect project
files first when their target framework or referenced package versions affect
the query.

Do not skip resolution because a likely package ID or API name is remembered.
Do not infer that an API from another version exists in the selected version.

## Create or change unit tests

Before creating or changing tests for a class that uses dependency injection:

1. Query `docs:company-docs` for the current unit-test template.
2. Follow that template and use Moq for injected services, repositories,
   clients, clocks, and other collaborators.
3. Avoid handwritten fakes, stubs, or test implementations when Moq can
   express the behavior.
4. Verify behaviorally significant interactions, including call counts,
   arguments, cancellation tokens, and calls that must not occur.

Use real implementations only for intentional integration tests, such as
SQLite persistence or ASP.NET Core hosting.
