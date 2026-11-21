# ADR-001: Lab 1 Backend Modular Monolith Architecture (.NET 10)

- **Status:** Accepted
- **Date:** 2026-11-21
- **Scope:** R1 Backend (`backend/`) for Labs 1–2 and transition path to Lab 3 Microservices

## Context

The Online Banking System must be delivered incrementally across four laboratory works:

1. **Lab 1:** Monolith API with registration/login (JWT), client/admin roles, bank account opening and balance queries, single-DB-transaction fund transfers, and operation history backed by PostgreSQL.
2. **Lab 2:** Containerized deployment adding asynchronous `PaymentCompleted` / `PaymentFailed` events via RabbitMQ.
3. **Lab 3:** Extraction into independent microservices (`client-service`, `account-service`, `transaction-service`, `notification-service`) benchmarked against the monolith under identical load and cloud resources.

We need an architecture that stays simple to build and run in Lab 1 while keeping domain boundaries strict enough that Lab 3 extraction does not require rewriting business logic, and keeping the PostgreSQL schema reusable for database load-testing coursework (`Software-Testing`).

## Decision

### 1. Modular Monolith Layout (`backend/src/`)

- **`OnlineBanking.Shared`** (`net10.0`, no ASP.NET Core or EF Core dependencies): `Result`/`DomainError`, `IDateTimeProvider`, and cross-module contracts (`IClientsApi`, `IAccountsApi`). In Labs 1–2 these are in-process adapters; in Lab 3 they become HTTP clients behind the same interfaces.
- **`OnlineBanking.Api`**: thin composition root (`Program.cs`, `HealthController`, CORS, OpenAPI, ProblemDetails, startup migration/seed toggles).
- **`OnlineBanking.Infrastructure`**: shared cross-cutting concerns (`InfrastructureInjection.cs`, JWT token generation, password hashing, `ICurrentUser`, `GlobalExceptionHandler`, `Result` -> RFC 7807 `ProblemDetails` mapping).
- **`OnlineBanking.Persistence`**: single `OnlineBankingDbContext` (EF Core 10 + Npgsql), entity configurations, repository implementations, EF Core migrations, idempotent SQL script export, and `DatabaseSeeder`.
- **Business modules (`OnlineBanking.Module.Clients`, `OnlineBanking.Module.Accounts`, `OnlineBanking.Module.Transactions`)**:
  - Each module is organized into `Api/` (MVC Controllers), `Application/` (services, DTOs, repository interfaces, shared contract adapters), and `Domain/` (entities, enums, domain invariants).
  - Modules never reference one another or `OnlineBanking.Persistence`. Architectural boundaries and absence of `MediatR`/`AutoMapper` are enforced automatically via `NetArchTest.Rules` in `OnlineBanking.UnitTests`.

### 2. Persistence & Isolated PostgreSQL Schemas

Lab 1 uses a single `OnlineBankingDbContext` in `OnlineBanking.Persistence` to keep one migration pipeline and simple local/CI database bootstrapping, while isolating tables into three PostgreSQL schemas:

- `clients.clients`
- `accounts.accounts`
- `transactions.transactions`

There are **zero cross-schema foreign keys** (`accounts.client_id` and `transactions.from_account_id`/`to_account_id` are logical UUID references without DB-level FK constraints). This allows extracting each schema into an independent database per service in Lab 3 without untangling cross-table constraints.

### 3. Atomic & Idempotent Transfer Flow

1. `Transactions` checks optional `Idempotency-Key` (returns existing record if already processed for that client).
2. `Transactions` validates source and destination accounts via `IAccountsApi`, persists a `Pending` record in `transactions.transactions`, and calls `IAccountsApi.ExecuteTransferAsync`.
3. `Accounts` / `Persistence` executes the balance update inside a single PostgreSQL transaction: locks both account rows with `SELECT ... FROM accounts.accounts WHERE id IN (@a, @b) ORDER BY id FOR UPDATE` (deterministic lock ordering prevents deadlocks under concurrent bidirectional transfers), verifies active status, matching currency, and `available_balance >= amount`, then debits and credits atomically.
4. `Transactions` transitions the record to `Completed` (HTTP `201`) or `Failed` (HTTP `409 Conflict` with `ProblemDetails`).

## Alternatives Considered

- **Immediate microservices in Lab 1:** Rejected — premature operational overhead before Lab 1 requirements need it, and Lab 3 explicitly requires comparing monolith vs. microservices.
- **Unstructured single-project monolith (shared models & FKs):** Rejected — couples `Clients`, `Accounts`, and `Transactions`, making Lab 3 decomposition error-prone.
- **Separate `DbContext` per module from day 1:** Deferred to Lab 3 — three separate migration histories add unnecessary friction for Lab 1–2 deployment and `Software-Testing` schema reuse when schema-level isolation inside `OnlineBankingDbContext` already preserves clean boundaries.
- **MediatR & AutoMapper:** Rejected — commercial licensing changes and indirection boilerplate outweigh any benefit for a small, explicit service layer. Explicit services and direct DTO mapping keep control flow transparent and easy to debug.

## Consequences

- **Positive:** Fast Lab 1 delivery, single migration stream, deadlock-safe atomic transfers, zero-warning `Release` builds, and verifiable module isolation via `NetArchTest.Rules`.
- **Trade-off:** Repository abstractions sit in module `Application` layers and are implemented in `OnlineBanking.Persistence` until Lab 3 splits each module into its own service and database.
