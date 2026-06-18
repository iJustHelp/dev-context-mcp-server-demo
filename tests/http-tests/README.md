# City API HTTP tests

Runnable REST Client requests (one file per endpoint, happy path only) for the
four `/city` endpoints. They work in the VS Code REST Client extension, Visual
Studio, and JetBrains Rider.

## Prerequisites

Run one implementation's API before sending requests, for example:

```bash
dotnet run --project claude/STI.City.API
```

## Host selection

The host is parameterized through the shared [`http-client.env.json`](http-client.env.json)
environment file, so the same `.http` files can target either implementation:

| Environment | Host |
| --- | --- |
| `claude` (default) | `http://localhost:5067` |
| `copilot` | `http://localhost:5178` |

Pick the environment in your editor's REST Client environment selector, then
send any request. The default host is `http://localhost:5067` (claude).

## Files

| File | Request |
| --- | --- |
| [city-list.http](city-list.http) | `GET /city` |
| [usa-cities.http](usa-cities.http) | `GET /city/usa` |
| [city-location.http](city-location.http) | `GET /city/New%20York/location` |
| [city-population.http](city-population.http) | `GET /city/New%20York/population` |

These are happy-path smoke checks. The `.http` format does not support response
assertions, so each file documents the expected `200 application/json` response
in a comment. Error-path requests (404 unknown city, 404 missing population,
502 upstream failure) are out of scope.
