# Repository Guidance

## DevContext must be up first

Before **planning**, **scaffolding**, or **coding** anything that depends on
internal NuGet packages, unfamiliar .NET APIs, company documentation, or
implementation examples:

1. Confirm the `dev_context` MCP server configured in [.mcp.json](.mcp.json) is
   connected and reachable.
2. Verify connectivity with a lightweight tool call (for example
   `resolve_library` for a package you will use.
3. If the server is down, misconfigured, or calls time out: **stop**. Do not
   draft implementation plans, write production code, or create unit tests that
   assume internal package or company-standard APIs. Start or fix
   [dev-context-mcp-server](https://github.com/iJustHelp/dev-context-mcp-server)
   using the endpoint in `.mcp.json`, then resume.

## Using DevContext during work

Use the repository-scoped `$dev-context` skill for internal NuGet packages,
unfamiliar .NET APIs, company documentation or standards, and implementation
examples.

The skill's live DevContext evidence is authoritative. If the skill or
`dev_context` MCP server becomes unavailable mid-task, or a result is
`not_found`, `insufficient_evidence`, or `not_ready`, do not invent APIs or
company rules. State the uncertainty and inspect the local repository for
additional evidence before continuing dependent implementation work.
