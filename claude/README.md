# City API — Claude implementation

A .NET 10 ASP.NET Core **Minimal API** implementing the functional spec in
[../design/spec-claude.md](../design/spec-claude.md). It serves global and U.S.
city lists from the internal `Demo.Cities` package, retrieves per-city geocoding
details from Open-Meteo via `OpenMeteo.Api.Client`, and caches those results in
SQLite (cache-aside) so repeated `/location` and `/population` lookups for the
same city do not call Open-Meteo again.

## Endpoints

| Method & route | Returns | Status codes |
| --- | --- | --- |
| `GET /city` | JSON `string[]` of all city names (alphabetical) | `200` |
| `GET /city/usa` | JSON `string[]` of U.S. city names (alphabetical) | `200` |
| `GET /city/{cityName}/location` | `{ cityName, country, latitude, longitude }` | `200`, `404`, `502` |
| `GET /city/{cityName}/population` | `{ cityName, country, population }` | `200`, `404`, `502` |

`{cityName}` is URL-decoded by routing, trimmed, and matched case-insensitively
against `Demo.Cities`. Errors are returned as Problem Details JSON:

- `404` — city not in `Demo.Cities` (FR-6), Open-Meteo returns no matching result
  (FR-13), or a matched city has no population value (OQ-3, treated as missing data).
- `502` — the Open-Meteo call fails and nothing is cached (FR-14).
- `500` — unexpected internal error.

## Projects

| Project | Responsibility |
| --- | --- |
| `STI.City.Core` | Domain model (`CityGeocoding`), abstractions (`ICityCatalog`, `IGeocodingProvider`, `IGeocodingCacheRepository`), the cache-aside orchestration (`CityGeocodingService`), the `Demo.Cities` catalog adapter, and `IClock`. |
| `STI.City.Data` | `SqliteGeocodingCacheRepository` (Dapper + Microsoft.Data.Sqlite) and `OpenMeteoGeocodingProvider` (adapts `IOpenMeteoClient`). |
| `STI.City.API` | Minimal API host: startup, DI, configuration, endpoint mapping, response DTOs. |
| `STI.City.Tests` | xUnit unit tests (Moq), a real-SQLite repository integration test, and `WebApplicationFactory` endpoint tests. |

The two detail endpoints share one code path (`CityGeocodingService.GetGeocodingAsync`)
and therefore one cached record (FR-10). The cache key is the normalized city name
via `Demo.Cities.Extensions.ToCityName`, and `NormalizedName` is the SQLite primary
key, enforcing one record per city (FR-11).

## Package contracts (verified via DevContext)

The internal/external package APIs were confirmed through the `dev_context` MCP
server rather than guessed:

- **`Demo.Cities` 2.1.1 (QA feed)** — `ICityService.GetCityNames()` and the
  **QA-only** `IUsaCityService.GetCityNames()` (both `IReadOnlyList<string>`,
  alphabetical), `Extensions.ToCityName(string)`, and `AddDemoCities()`.
  The QA package is required: the prod `Demo.Cities` `1.0.0` does not expose
  `IUsaCityService`, so `GET /city/usa` cannot be satisfied by it.
  `nuget://qaNuget/Demo.Cities/2.1.1/symbol/Demo.Cities.IUsaCityService`
- **`OpenMeteo.Api.Client` 1.0.0 (prod feed)** —
  `IOpenMeteoClient.SearchLocationsAsync(name, count, language, format, ct)` →
  `GeocodingResponse.Results` (`ICollection<LocationResult>`), where
  `LocationResult` has `Name`, `Country`, `Latitude`/`Longitude` (`double`) and
  nullable `Population`. Failures surface as `ApiException`. Registered with
  `AddOpenMeteoApiClient(...)`.
  `nuget://prodNuget/OpenMeteo.Api.Client/1.0.0/symbol/OpenMeteo.Api.Client.IOpenMeteoClient.SearchLocationsAsync`

### Note on `Formula.SimpleRepo`

The spec names `Formula.SimpleRepo` for SQLite persistence, but its detailed
repository API returned **`insufficient_evidence`** from `dev_context` (the
package version is indexed, but no usable `Repo<>` subclassing/registration
example), and the spec itself flags this as unverified. Per repository guidance
("do not invent APIs when results are `insufficient_evidence`"), the cache is
implemented on the **same foundation `Formula.SimpleRepo` wraps — Dapper over
`Microsoft.Data.Sqlite`** — behind the `IGeocodingCacheRepository` abstraction in
`STI.City.Core`. Swapping in a `Formula.SimpleRepo`-based repository later is a
drop-in replacement of that one class, with no change to the API or domain code.

## Configuration

| Setting | Purpose | Default |
| --- | --- | --- |
| `ConnectionStrings:CityCache` | SQLite cache connection string (**required**; startup fails if blank) | `Data Source=city-cache.db` |
| `OpenMeteo:BaseAddress` | Open-Meteo geocoding base address | `https://geocoding-api.open-meteo.com/` |

## Build, test, run

Requires the **.NET 10 SDK**. From the repository root:

```bash
# Build + run all tests (unit, SQLite integration, API endpoint tests)
dotnet test claude/City.slnx

# Run the API (listens on http://localhost:5067)
dotnet run --project claude/STI.City.API
```

With the API running, the smoke requests in [../http-tests/](../http-tests/)
exercise each endpoint.

> NuGet restore uses [`nuget.config`](nuget.config): nuget.org plus the local
> DevContext demo feeds for the internal packages. Restore emits a transitive
> advisory warning (`NU1903`) for `SQLitePCLRaw.lib.e_sqlite3` pulled in by
> `Microsoft.Data.Sqlite`; it is informational and does not affect the build.

## Traceability

Each functional requirement and acceptance scenario from the spec is covered by a
test: catalog ordering and case-insensitive matching (`CityCatalogTests`),
cache-aside orchestration including cache hit/miss, shared record, unknown city,
empty result, and upstream failure (`CityGeocodingServiceTests`), real SQLite
round-trip and single-record-per-city (`SqliteGeocodingCacheRepositoryTests`),
and end-to-end status-code mapping for all four endpoints (`CityApiEndpointsTests`).
