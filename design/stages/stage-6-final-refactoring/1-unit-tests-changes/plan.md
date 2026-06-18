# Company-Template Unit Test Refactor

## Summary
Refactor `CityGeocodingServiceTests` to follow `docs://company-docs/test-template.instructions.md`. Preserve all current behavior and coverage.

## Implementation
- Replace hand-written fakes with `private readonly Mock<T>` fields for all injected collaborators, including `TimeProvider` and `ILogger`.
- Add constructor-created `private readonly CityGeocodingService _target`.
- Rename tests to `MethodUnderTest_Condition_ExpectedResult`.
- Add mandatory `// Purpose:`, `// arrange`, `// act`, and `// assert` comments.
- Keep each act section to one `_target` invocation and name returned values `actual`.
- Split the unknown-city theory so blank input and unrecognized cities can verify different interaction paths precisely.
- Verify every expected call, argument, call count, and cancellation token.
- Verify important forbidden calls with `Times.Never`.
- Verify warning logs for API, transport, and timeout failures.
- Add a helper that calls `VerifyNoOtherCalls()` on every mock.
- Move the DI lifetime assertion into a focused registration test class because it does not exercise `_target`.

## Boundaries
- Keep endpoint hosting, SQLite repository, and startup tests unchanged because they are intentional integration tests.
- Make no production-code or public-API changes.
- Keep the existing xUnit and Moq package versions.

## Verification
- Run `dotnet test tests/STI.City.Tests/STI.City.Tests.csproj --no-restore`.
- Preserve the current baseline of 33 passing tests.
- Confirm no hand-written injected test doubles remain in service unit tests.
- Confirm every service test follows the company naming, layout, setup, and verification rules.
