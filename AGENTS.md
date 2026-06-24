# Repository Guidance

Use the repository-scoped `$dev-context` skill for internal NuGet packages,
unfamiliar .NET APIs, company documentation or standards, implementation
examples, and all unit-test creation or changes for dependency-injected
classes.

The skill's live DevContext evidence is authoritative. If the skill or
`dev_context` MCP server is unavailable, or a result is `not_found`,
`insufficient_evidence`, or `not_ready`, do not invent APIs or company rules.
State the uncertainty and inspect the local repository for additional evidence
before continuing dependent implementation work.
