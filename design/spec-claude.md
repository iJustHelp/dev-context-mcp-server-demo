# City API — Functional Specification

## Overview

The **City API** is a .NET 10 Minimal API that exposes city lists and per-city
geocoding details (location and population). City names come from internal
`Demo.Cities` services; geocoding details come from the Open-Meteo geocoding
service via `OpenMeteo.Api.Client` and are cached in SQLite so repeated detail
lookups for the same city do not call Open-Meteo again.

This document is a **functional specification**: it defines the system's
externally observable behavior — endpoint contracts, input handling, status
codes, and acceptance scenarios — so QA and implementers have a single testable
reference. It is derived strictly from [brd.md](brd.md).

### Scope

In scope:

- Behavior of the four HTTP endpoints (inputs, outputs, status codes).
- Input validation and city-name normalization rules.
- Caching behavior expressed as observable outcomes.
- Acceptance scenarios and traceability to the BRD.

Out of scope:

- Internal class design, project layout, and DI wiring — see
  [stages.md](stages.md) and `stages/*/plan.md`.
- Database schema / DDL — only the functional set of persisted fields is
  described here.
- Package selection mechanics beyond the functional constraints they impose.

### Source of truth and conventions

- All requirements derive from [brd.md](brd.md). Where the BRD is silent, the
  behavior is listed under [Open Questions](#open-questions--unspecified-behavior)
  rather than assumed.
- Package facts were verified through the `dev_context` MCP server; citation
  URIs are included where a behavior depends on a verified package API.
- Functional requirements are numbered `FR-n` and acceptance scenarios `AS-n`
  for traceability.

---

## Actors & Use Cases

**Actor — API consumer:** any client application that issues HTTP requests to
the City API. There is one actor; no authentication or roles are defined in the
BRD.

| Use case | Description | Endpoint |
| --- | --- | --- |
| UC-1 List all cities | Retrieve every known city name | `GET /city` |
| UC-2 List U.S. cities | Retrieve U.S. city names | `GET /city/usa` |
| UC-3 Get city location | Retrieve a city's latitude/longitude | `GET /city/{cityName}/location` |
| UC-4 Get city population | Retrieve a city's population | `GET /city/{cityName}/population` |

---

## Functional Requirements

### Data sources

- **FR-1** — `GET /city` returns the city names provided by
  `Demo.Cities.ICityService.GetCityNames()`.
- **FR-2** — `GET /city/usa` returns the U.S. city names provided by
  `Demo.Cities.IUsaCityService.GetCityNames()`. This interface exists only in
  the **QA** `Demo.Cities` package; the prod package does not expose it (see
  [Dependencies](#dependency-constraints)).
- **FR-3** — Geocoding details (coordinates and population) are obtained from
  `OpenMeteo.Api.Client.IOpenMeteoClient`. A single geocoding result supplies
  both the coordinates used by `/location` and the population used by
  `/population`.

### City-name handling

- **FR-4** — For the parameterized routes, the `{cityName}` path segment is
  **URL-decoded** before use.
- **FR-5** — City names are matched **case-insensitively** against
  `Demo.Cities`.
- **FR-6** — A request whose (decoded, case-insensitively matched) city is not
  present in `Demo.Cities` yields `404 Not Found` and does **not** call
  Open-Meteo.

### Ordering

- **FR-7** — `GET /city` and `GET /city/usa` preserve the package-provided
  **alphabetical** order of names. (Both `GetCityNames()` methods are
  documented to return names in alphabetical order; the API must not reorder
  them.)

### Caching (observable behavior)

- **FR-8** — Geocoding lookups use a **cache-aside** strategy: the normalized
  city name is checked in SQLite first; on a hit, the cached result is used and
  Open-Meteo is **not** called.
- **FR-9** — On a cache **miss**, the API calls Open-Meteo and persists the
  selected geocoding result before responding.
- **FR-10** — The `/location` and `/population` endpoints for the same city
  share the **same** cached geocoding record. After the record is cached (by
  either endpoint), subsequent `/location` **and** `/population` requests for
  that city are served from cache without calling Open-Meteo.
- **FR-11** — At most **one** cache record exists per normalized city name.

### Status outcomes

- **FR-12** — Endpoints return `200 OK` with a JSON body when data is
  available.
- **FR-13** — A detail endpoint returns `404 Not Found` when Open-Meteo returns
  **no matching result** for a city that exists in `Demo.Cities` (empty
  `GeocodingResponse.Results`).
- **FR-14** — A detail endpoint returns `502 Bad Gateway` when the Open-Meteo
  call **fails** and there is **no cached result** available to serve.
- **FR-15** — All endpoints return **JSON**.

---

## Endpoint Specifications

### `GET /city`

| Field | Value |
| --- | --- |
| Method / Path | `GET /city` |
| Path params | none |
| Query params | none |
| Source | `ICityService.GetCityNames()` |
| Success | `200 OK`, JSON list of city names |
| Order | Package-provided alphabetical order, preserved (FR-7) |
| Failure | none defined in the BRD |

### `GET /city/usa`

| Field | Value |
| --- | --- |
| Method / Path | `GET /city/usa` |
| Path params | none |
| Query params | none |
| Source | `IUsaCityService.GetCityNames()` (QA package only) |
| Success | `200 OK`, JSON list of U.S. city names |
| Order | Package-provided alphabetical order, preserved (FR-7) |
| Failure | none defined in the BRD |

### `GET /city/{cityName}/location`

| Field | Value |
| --- | --- |
| Method / Path | `GET /city/{cityName}/location` |
| Path params | `cityName` — URL-decoded (FR-4), matched case-insensitively (FR-5) |
| Query params | none |
| Source | Cached geocoding record, else `IOpenMeteoClient` (FR-3, FR-8, FR-9) |
| Success | `200 OK`, JSON containing the matched city's **latitude** and **longitude** |
| `404` | City not in `Demo.Cities` (FR-6), or Open-Meteo returns no matching result (FR-13) |
| `502` | Open-Meteo fails and no cached result is available (FR-14) |

### `GET /city/{cityName}/population`

| Field | Value |
| --- | --- |
| Method / Path | `GET /city/{cityName}/population` |
| Path params | `cityName` — URL-decoded (FR-4), matched case-insensitively (FR-5) |
| Query params | none |
| Source | Same cached geocoding record as `/location` (FR-10) |
| Success | `200 OK`, JSON containing the matched city's **population** |
| `404` | City not in `Demo.Cities` (FR-6), or Open-Meteo returns no matching result (FR-13) |
| `502` | Open-Meteo fails and no cached result is available (FR-14) |
| Note | `LocationResult.Population` is **nullable** in the client; behavior when a matched result has no population is **unspecified** — see [Open Questions](#open-questions--unspecified-behavior) |

---

## Input Validation & Normalization Rules

- **VR-1** — The `{cityName}` path segment is URL-decoded before any matching
  (FR-4).
- **VR-2** — Matching against `Demo.Cities` is case-insensitive (FR-5).
- **VR-3** — A **normalized** form of the city name is used as the cache key.
  Per the BRD, the cache stores the normalized city name and enforces one record
  per normalized name (FR-11). (The QA `Demo.Cities` package provides a
  `Demo.Cities.Extensions.ToCityName(string)` helper that produces a canonical
  name; whether that exact helper is the normalizer is an implementation detail
  and not mandated here.)
- **VR-4** — Behavior for **malformed** URL-encoding in `{cityName}` is
  unspecified — see [Open Questions](#open-questions--unspecified-behavior).

---

## Status Code Matrix

| Condition | `/city` | `/city/usa` | `/location` | `/population` |
| --- | --- | --- | --- | --- |
| Data available | `200` | `200` | `200` | `200` |
| City not in `Demo.Cities` | n/a | n/a | `404` | `404` |
| Open-Meteo returns no matching result | n/a | n/a | `404` | `404` |
| Open-Meteo fails **and** no cached result | n/a | n/a | `502` | `502` |

All success responses are JSON (FR-15). The BRD defines no failure response for
the two list endpoints.

---

## Cached Data — Functional View

The cache persists, at minimum, the following fields per the BRD. They are
listed here because consumer-visible behavior depends on them (e.g., `/location`
and `/population` both read from the same record):

| Field | Purpose |
| --- | --- |
| Normalized city name | Cache key; enforces one record per city (FR-11) |
| Display name | Human-readable name from the geocoding result |
| Country | From the geocoding result |
| Latitude | Served by `/location` |
| Longitude | Served by `/location` |
| Population | Served by `/population` (nullable upstream) |
| Retrieval timestamp | When the result was fetched/persisted |

**Cache-reuse guarantee:** once a city's record is cached, both detail endpoints
serve it without contacting Open-Meteo (FR-8, FR-10). This is a functional view
only — no schema or DDL is specified here.

---

## Acceptance Scenarios

Given/When/Then scenarios, each traceable to FRs and the BRD acceptance
criteria.

- **AS-1 — List all cities** *(FR-1, FR-7, FR-15)*
  - **Given** the API is running
  - **When** the consumer calls `GET /city`
  - **Then** it returns `200` with a JSON list of all `ICityService` names in
    the package's alphabetical order.

- **AS-2 — List U.S. cities** *(FR-2, FR-7, FR-15)*
  - **Given** the API is running with the QA `Demo.Cities` package
  - **When** the consumer calls `GET /city/usa`
  - **Then** it returns `200` with a JSON list of `IUsaCityService` names in the
    package's alphabetical order.

- **AS-3 — Successful location lookup (cache miss)** *(FR-3, FR-9, FR-12)*
  - **Given** a city that exists in `Demo.Cities` and is not yet cached
  - **When** the consumer calls `GET /city/{cityName}/location`
  - **Then** the API calls Open-Meteo, persists the result, and returns `200`
    with the city's latitude and longitude.

- **AS-4 — Successful population lookup (cache miss)** *(FR-3, FR-9, FR-12)*
  - **Given** a city that exists in `Demo.Cities` and is not yet cached
  - **When** the consumer calls `GET /city/{cityName}/population`
  - **Then** the API calls Open-Meteo, persists the result, and returns `200`
    with the city's population.

- **AS-5 — Cache hit avoids upstream call** *(FR-8)*
  - **Given** a city whose geocoding result is already cached
  - **When** the consumer calls either detail endpoint for that city
  - **Then** the API returns `200` from cache and does **not** call Open-Meteo.

- **AS-6 — Location and population share one record** *(FR-10, FR-11)*
  - **Given** a city not yet cached
  - **When** the consumer calls `/location` and then `/population` (or vice
    versa) for that city
  - **Then** Open-Meteo is called **at most once**, a single cache record is
    created, and the second request is served from that record.

- **AS-7 — Unknown city → 404** *(FR-6, FR-13)*
  - **Given** a `{cityName}` not present in `Demo.Cities`
  - **When** the consumer calls either detail endpoint
  - **Then** the API returns `404` and does **not** call Open-Meteo.

- **AS-8 — Empty Open-Meteo result → 404** *(FR-13)*
  - **Given** a city in `Demo.Cities` for which Open-Meteo returns an empty
    `Results` collection
  - **When** the consumer calls either detail endpoint (cache miss)
  - **Then** the API returns `404`.

- **AS-9 — Upstream failure with no cache → 502** *(FR-14)*
  - **Given** a city in `Demo.Cities` with no cached result
  - **And** Open-Meteo fails (e.g., `ApiException`)
  - **When** the consumer calls either detail endpoint
  - **Then** the API returns `502`.

- **AS-10 — Case-insensitive / URL-encoded match** *(FR-4, FR-5)*
  - **Given** a known city referenced with different casing and/or URL-encoded
    characters (e.g., a space encoded as `%20`)
  - **When** the consumer calls a detail endpoint with that form
  - **Then** the API decodes and matches it to the same city and returns the
    same result as the canonical form.

---

## Traceability Matrix

Maps each BRD Acceptance Criterion ([brd.md:64-70](brd.md#L64-L70)) to covering
requirements and scenarios.

| BRD acceptance criterion | Functional requirements | Acceptance scenarios |
| --- | --- | --- |
| All four endpoints return JSON and follow the documented status codes | FR-12, FR-13, FR-14, FR-15 | AS-1…AS-9 |
| `/city` and `/city/usa` preserve package-provided alphabetical order | FR-7 | AS-1, AS-2 |
| Location and population share the same cached geocoding record | FR-3, FR-10, FR-11 | AS-6 |
| Repeating either detail request for a cached city does not call Open-Meteo | FR-8, FR-10 | AS-5, AS-6 |
| Tests cover city lists, successful lookups, cache hits, missing cities, empty Open-Meteo results, and upstream failures | FR-1…FR-14 | AS-1, AS-2, AS-3, AS-4, AS-5, AS-7, AS-8, AS-9 |

---

## Dependency Constraints

These functional constraints follow from the package APIs (verified via
`dev_context`):

- **QA `Demo.Cities` is required, not merely preferred.** Only the QA package
  exposes `IUsaCityService`; the prod `Demo.Cities` `1.0.0` exposes only
  `ICityService`. `GET /city/usa` (FR-2) therefore cannot be satisfied by the
  prod package.
  - Verified: `nuget://qaNuget/Demo.Cities/2.1.1/symbol/Demo.Cities.IUsaCityService`
  - Verified absent in prod: `nuget://prodNuget/Demo.Cities/1.0.0` exposes only
    `Demo.Cities.ICityService`.
- **Open-Meteo geocoding** is performed via
  `IOpenMeteoClient.SearchLocationsAsync(...)` returning a `GeocodingResponse`
  whose `Results` is a collection of `LocationResult`
  (`Latitude`/`Longitude`/`Country` and nullable `Population`). An empty
  `Results` maps to `404` (FR-13); a thrown `ApiException` with no cache maps to
  `502` (FR-14).
  - Verified: `nuget://prodNuget/OpenMeteo.Api.Client/1.0.0/symbol/OpenMeteo.Api.Client.IOpenMeteoClient.SearchLocationsAsync`
- **SQLite persistence** uses `Formula.SimpleRepo`. Its detailed repository API
  could **not** be verified through `dev_context` (`insufficient_evidence`); per
  repository guidance it must be confirmed against the local solution rather
  than inferred. This does not affect the functional contract above.

---

## Open Questions / Unspecified Behavior

The BRD does not define the following; they are gaps to resolve, not assumed
behavior:

- **OQ-1 — List response body shape.** Exact JSON shape of `/city` and
  `/city/usa` (bare array of strings vs. wrapped object) and field naming.
- **OQ-2 — Detail response field names/casing.** Exact JSON property names for
  latitude/longitude (`/location`) and population (`/population`).
- **OQ-3 — Null population.** `LocationResult.Population` is nullable. When a
  matched city has no population value, `/population` behavior is undefined —
  candidates: `200` with a null/absent value, or `404`.
- **OQ-4 — Malformed URL-encoding.** Response when `{cityName}` contains invalid
  percent-encoding (e.g., `400` vs. treat-as-literal then `404`).
- **OQ-5 — Open-Meteo `count`/language.** Whether multiple geocoding results
  are requested and how the "selected result" is chosen when more than one is
  returned (the BRD says "the selected result" but not the selection rule).
- **OQ-6 — Cache staleness.** A retrieval timestamp is stored, but the BRD
  defines no expiry/refresh; cached records are assumed to live indefinitely.
