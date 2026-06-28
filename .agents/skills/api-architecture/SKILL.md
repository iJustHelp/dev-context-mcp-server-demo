---
name: api-architecture
description: >-
  Defines STI API project structure, dependency direction, and service/repository
  patterns. Use when creating or refactoring API solutions, controllers, services,
  repositories, or STI.{ApiName} projects.
---

# API Architecture

## Project Structure

Each API solution contains the following projects:

- `STI.{ApiName}.API` contains controllers, application startup, dependency
  injection registration, configuration, and Serilog setup.
- `STI.{ApiName}.Core` contains domain models, service contracts, service
  implementations, and repository contracts.
- `STI.{ApiName}.Data` contains database entities, persistence mappings, and
  repository implementations using the `Formula.SimpleRepo` NuGet package.
- `STI.{ApiName}.Tests` contains unit tests for services and focused tests for
  controllers and repositories.

## Dependency Direction

- `API` depends on `Core` and registers `Data` implementations through
  dependency injection.
- `Data` depends on repository contracts and domain models defined in `Core`.
- `Core` must not depend on `API`, database frameworks, or concrete repository
  implementations.
- Controllers must delegate business logic to services instead of accessing
  repositories directly.

## Services and Repositories

- Define an interface for each service that is consumed through dependency
  injection or replaced in unit tests.
- Keep business rules in Core services.
- Define repository interfaces in `Core` and implement them in `Data`.
- Keep database entities separate from API request and response models.
- Map database entities to domain models at the Data boundary.

## Testing

- Use `Unit Test Generation Standard.md` as unit test standard.
- Unit-test service behavior through service interfaces and mocked repository
  contracts.
- Use `Unit Test Generation Standard.md` document as unit test company standard.
- Test controllers for routing, validation, authorization, and response
  mapping.
- Add integration tests for repository queries and database mappings.
