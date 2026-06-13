# Stage 4: Implement HTTP Endpoints

## Goal

Expose the four specified JSON endpoints and map application outcomes to the
required HTTP contracts.

## Dependencies

- Stage 3 is complete.
- Map the four `CityGeocodingOutcome` values documented in the Stage 3 plan
  to the HTTP contracts in `design/spec.md`.

## Implementation Status

**Complete.** All four Minimal API endpoints, stable response DTOs, outcome
mapping, Problem Details responses with trace identifiers, endpoint metadata,
and centralized sanitized exception handling are implemented.

| Route | Success |
| --- | --- |
| `GET /city` | Exact `ICityService` list and order |
| `GET /city/usa` | Exact `IUsaCityService` list and order |
| `GET /city/{cityName}/location` | `CityLocationResponse` |
| `GET /city/{cityName}/population` | `CityPopulationResponse` |

## Work

- [x] Map `GET /city` to `ICityService.GetCityNames()` without reordering.
- [x] Map `GET /city/usa` to `IUsaCityService.GetCityNames()` without
  reordering.
- [x] Map `GET /city/{cityName}/location` to the shared geocoding service.
- [x] Map `GET /city/{cityName}/population` to the same service.
- [x] Define separate location and population response DTOs.
- [x] Return typed JSON results for success.
- [x] Return Problem Details for `404`, `502`, and `500` responses with a
  trace ID and no internal exception details.
- [x] Ensure the static `/usa` route cannot be captured as a city name.
- [x] Add endpoint names and OpenAPI response metadata.

## Deliverables

- All four public endpoints.
- Stable success DTOs and error contracts.
- Centralized exception handling for internal failures.

## Exit Criteria

- [x] Every endpoint returns the status, content type, and JSON shape
  documented in `design/spec.md`.
- [x] URL-encoded and mixed-case city names are passed correctly to the shared
  geocoding service.
- [x] Missing population returns `404` while the shared location record
  remains valid.
- [x] Upstream failure on a cache miss maps to `502`.
- [x] Internal persistence failures return sanitized `500` Problem Details.

## Verification

- Restore: succeeded offline using the QA feed, production feed, and existing
  global package cache.
- Build: succeeded with zero warnings and zero errors.
- Tests: 33 passed, 0 failed.
- Endpoint tests use strict Moq collaborators and verify route values, call
  counts, JSON contracts, content types, outcome mappings, trace identifiers,
  and sanitized internal errors.
