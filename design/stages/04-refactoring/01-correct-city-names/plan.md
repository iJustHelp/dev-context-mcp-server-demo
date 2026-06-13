# Correct City List Names

## Summary

Return display-cased city names from the two list endpoints while preserving package order and leaving internal matching/cache behavior unchanged.

Expected responses:

- `/city`: `Berlin`, `London`, `Paris`, `Tokyo`, `Toronto`
- `/city/usa`: `Chicago`, `Houston`, `Los Angeles`, `New York`, `Philadelphia`, `Phoenix`

## Implementation Changes

- Map each value returned by `ICityService.GetCityNames()` and `IUsaCityService.GetCityNames()` through the package’s verified `ToCityName()` extension before serialization.
- Keep the package services and their lowercase values unchanged.
- Do not change geocoding resolution, Open-Meteo requests, normalized cache keys, or detail endpoint display names.
- Update `design/spec.md` to explicitly require display-cased list responses while preserving package-provided ordering.
- Add this plan as `design/stages/04-refactoring/01-correct-city-names/plan.md`.

## API Impact

- Response shape and status remain unchanged: both endpoints still return `200` JSON string arrays.
- Only string casing changes; ordering and membership remain identical.
- No package, database schema, or public C# interface changes are required.

## Test Plan

- Update list endpoint tests to assert the exact display-cased arrays above.
- Retain assertions for `application/json`, response shape, and ordering.
- Run all existing detail endpoint and cache tests to prove lowercase canonical matching and cache behavior remain intact.
- Build the Release solution with zero warnings and run the complete test suite.

## Assumptions

- “Correct city name” means title casing through `Demo.Cities.Extensions.ToCityName()`.
- Historical stage plans remain unchanged; the updated specification and this refactoring plan supersede the earlier direct-return requirement.
- Existing unrelated working-tree changes and `city-cache.db` are not modified.
