---
name: dev-context
description: Research internal NuGet packages, unfamiliar .NET APIs, public symbols, implementation examples, and company engineering standards through the dev_context MCP server. Use before implementing or reviewing code that depends on internal packages or uncertain .NET APIs, when company documentation or architecture guidance is needed, and whenever creating or changing unit tests for dependency-injected classes. Do not use for unrelated repository work whose APIs and standards are already established locally.
---

# DevContext

Use the live `dev_context` MCP server as the source of truth. Inspect project
files first when their target framework or referenced package versions affect
the query.

## Research NuGet libraries

1. Call `resolve_library` first with the package, client, type, member, or
   implementation concept.
2. Call `list_versions` for the resolved NuGet library. Prefer the version
   already referenced by the project; otherwise select a version compatible
   with the project's target framework and constraints.
3. Call `query_docs` for implementation guidance, examples, warnings,
   registration patterns, and usage conventions.
4. Call `get_symbol` only when an exact public type or member signature is
   required.
5. Use the resulting evidence to guide the implementation. Preserve relevant
   citation URIs in specifications, plans, code comments, or the final report
   when they materially support a decision.

Do not skip resolution because a likely package ID or API name is remembered.
Do not infer that an API from another version exists in the selected version.

## Research company documentation

Resolve or query `docs:company-docs`. Use `query_docs` with a focused question
for architecture, testing, naming, implementation, and other company
standards.

Do not call `list_versions` or `get_symbol` for company documentation.

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

## Handle incomplete evidence

Treat `not_found`, `insufficient_evidence`, `not_ready`, missing documentation,
and MCP failures as unresolved evidence. Do not invent APIs, signatures,
registration methods, or company rules.

State the uncertainty and any important warnings. Inspect the local repository,
project assets, or restored package metadata for additional evidence when
available. If the requested implementation still depends on an unverified API,
stop that portion of the work and report what must be confirmed.
