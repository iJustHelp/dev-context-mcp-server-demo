Plan: Create Functional Spec from brd.md
Context
The design/brd.md Business Requirements Document describes the City API — a .NET 10 Minimal API exposing city lists and geocoding details, backed by internal NuGet packages and a SQLite cache. The BRD captures what the system must do at a business level but is not structured as a testable functional specification (no use cases, no per-endpoint contract tables, no Given/When/Then acceptance scenarios, no consolidated status-code/validation matrices).

This task produces an exhaustive, behavior-focused functional specification at design/spec.md. It is a documentation-only change: it translates the BRD into precise, verifiable functional requirements and acceptance scenarios that QA and implementers can test against. It does not change any code. Internal design ("how") is deliberately out of scope — that lives in the BRD's caching notes and the design/stages/* plans.

Source of truth
All content is derived strictly from design/brd.md. Where the BRD is silent on a behavior, the spec will mark it explicitly as Unspecified (see BRD) rather than inventing behavior. No package APIs will be invented — the spec describes observable behavior, not implementation.

Deliverable
A single new file: design/spec.md.

It does not modify brd.md, the stages, or any source under claude/ or codex/.

spec.md structure (exhaustive, functional)
Overview — one-paragraph product summary, link back to brd.md, and an explicit Scope / Out-of-Scope list (in: endpoint behavior, validation, status codes, acceptance scenarios; out: internal class design, package selection mechanics).

Actors & Use Cases

Actor: API consumer (client application).
Use cases: list all cities, list U.S. cities, get a city's location, get a city's population.
Functional Requirements (numbered FR-1, FR-2, …) — atomic, testable statements covering: data sources (Demo.Cities.ICityService, QA-only Demo.Cities.IUsaCityService, OpenMeteo.Api.Client.IOpenMeteoClient), name handling (URL-decode + case-insensitive match), alphabetical ordering preservation, and the cache-aside behavior expressed as observable outcomes (cache hit does not call Open-Meteo; location & population share one cached record).

Endpoint Specifications — one subsection per endpoint, each with a contract table (Method, Path, Path/Query params, Success body shape, Status codes, Notes):

GET /city
GET /city/usa
GET /city/{cityName}/location
GET /city/{cityName}/population Each documents JSON response, the alphabetical-order guarantee where it applies, and name normalization for the parameterized routes.
Input Validation & Normalization Rules — URL-decoding, case-insensitive matching, normalized-name concept (as the cache key per BRD), one record per normalized name.

Status Code Matrix — consolidated table mapping conditions to codes: 200 (data available), 404 (city not in Demo.Cities, or Open-Meteo has no matching result), 502 (Open-Meteo fails and no cached result available).

Cached Data — Functional View — the fields the system must persist as stated in the BRD (normalized name, display name, country, latitude, longitude, population, retrieval timestamp) framed as data the consumer's behavior depends on, plus the cache-reuse guarantee. (Functional view only — no schema/DDL.)

Acceptance Scenarios (Given/When/Then) — exhaustive, traceable to the BRD Acceptance Criteria and FRs. At minimum: city list success & ordering, USA list success & ordering, successful location lookup, successful population lookup, cache hit avoids upstream call, location+population share record, unknown city → 404, empty Open-Meteo result → 404, upstream failure with no cache → 502, case-insensitive / URL-encoded name match.

Traceability Matrix — table linking each BRD Acceptance Criterion (brd.md lines 64–70) to the FR(s) and acceptance scenario(s) that cover it, so no requirement is dropped.

Open Questions / Unspecified Behavior — anything the BRD does not pin down (e.g., exact JSON field names/casing, response body for list endpoints, behavior on malformed URL-encoding), listed as explicit gaps rather than assumed.

Notes on approach
Tone and Markdown style will match the existing design/ docs (heading levels, tables, checkbox/bullet conventions seen in stages/*/plan.md).
Every functional requirement and acceptance scenario will be uniquely numbered for traceability.
The spec stays at the functional/behavioral altitude; it will cross-reference but not duplicate the stage plans.
Verification
Since this is documentation only:

Open design/spec.md and confirm it renders cleanly (tables, headings).
Cross-check the Traceability Matrix: every BRD Acceptance Criterion (brd.md:64–70) maps to at least one FR and one acceptance scenario.
Confirm each of the 4 endpoints and all three status codes (200/404/502) from the BRD appear in the endpoint specs and status-code matrix.
Confirm no invented APIs or internal-design details leaked in (functional altitude maintained), and that BRD gaps are listed under Open Questions rather than silently resolved.