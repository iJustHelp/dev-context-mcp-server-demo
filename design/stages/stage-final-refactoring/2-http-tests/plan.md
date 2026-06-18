# Add HTTP tests against each City API endpoint


## Goal

Provide runnable .http REST Client files (one per endpoint, happy path only) that hit the four /city endpoints. They work in VS Code REST Client, Visual Studio, and Rider. Because the two implementations listen on different dev ports (claude 5067, copilot 5178), the host is parameterized via a shared environment file so the same tests target either implementation.

## Endpoint contracts (recovered from git HEAD, since source is deleted):

Endpoint	Success response
GET /city	string[] of all city names
GET /city/usa	string[] of U.S. city names
GET /city/{cityName}/location	{ cityName, country, latitude, longitude }
GET /city/{cityName}/population	{ cityName, country, population }
Changes
All new files live under http-tests/.

1. http-tests/http-client.env.json
Shared environment file (read by VS Code REST Client and Visual Studio). Defines a host variable per environment so one set of .http files targets either implementation:

{
  "$shared": { "host": "http://localhost:5067" },
  "claude":  { "host": "http://localhost:5067" },
  "copilot": { "host": "http://localhost:5178" }
}
2. Four .http files, one per endpoint (happy path only)
Each references {{host}} and an Accept: application/json header, with a comment noting the expected 200 response.

http-tests/city-list.http → GET {{host}}/city
http-tests/usa-cities.http → GET {{host}}/city/usa
http-tests/city-location.http → GET {{host}}/city/New%20York/location (URL-encoded city name, per spec matching rules)
http-tests/city-population.http → GET {{host}}/city/New%20York/population
Example shape:

@host = {{host}}

### GET all city names -> 200 application/json, string[]
GET {{host}}/city
Accept: application/json
3. http-tests/README.md
Short usage doc: prerequisites (run one implementation's API, e.g. dotnet run --project claude/STI.City.API), how to pick the claude/copilot environment in the REST Client, the default host, and that these are happy-path smoke checks (assertions are not part of the .http format).

4. Fill in the repo stage plan
Replace the "add" stub in design/stages/stage-final-refactoring/2-http-tests/plan.md with a concise stage plan matching the repo's existing plan style (Summary / Implementation / Verification), describing this .http test suite.

### Notes

Out of scope (per the user's "happy path only" choice): error-path requests (404 unknown city, 404 missing population, 502 upstream failure).