# ADR-002: Lab 1 Frontend Architecture (React 19, TypeScript, and Vite)

- **Status:** Accepted
- **Date:** 2026-11-22
- **Scope:** R2 Frontend (`frontend/`) and R2-owned API integration tests for Lab 1

## Context

Lab 1 needs a usable web client for the existing modular-monolith API. The
frontend must support client registration and login, JWT-authenticated account
and transfer operations, transaction history, and an administrator view. It
must remain independently runnable while R1 and R3 continue work on the API
and infrastructure.

The API source of truth is
[`docs/openapi/lab1-monolith.yaml`](../openapi/lab1-monolith.yaml). In
particular, API authorization is based on the bearer JWT; client-only and
admin-only operations are enforced by the server, not by the user interface.

## Decision

### 1. Browser application

- Build a single-page application with React 19, TypeScript, and Vite. Keep the
  implementation in the `frontend/` project rather than adding a second
  frontend service or a new backend-for-frontend.
- Provide login/registration, account overview and management, internal
  transfers, paginated/filterable history, and an admin-only client list with
  status management.
- Keep the UI responsive and use native forms/controls, meaningful labels, and
  visible loading, success, and error states.

### 2. API boundary and authentication

- Keep all HTTP calls in `frontend/src/api.ts`; TypeScript interfaces in
  `frontend/src/types.ts` reflect the OpenAPI payloads used by the application.
- Send the access token as `Authorization: Bearer ...` for protected endpoints.
  Store the token and its expiry in browser local storage, remove expired or
  invalid saved sessions, and provide an explicit logout action.
- Render the admin navigation only for an `Admin` session, but rely on API
  authorization for actual protection. Do not treat frontend role checks as a
  security control.
- Convert API Problem Details responses into visible messages and report
  connection failures instead of showing success-shaped fallbacks.
- Generate an `Idempotency-Key` for every transfer request. Do not retry
  financial requests automatically.

The browser calls the API directly. Local Vite and Docker UI both use
`http://localhost:5173`, which is already included in the backend development
CORS configuration. Deployments using another origin must configure
`Cors:AllowedOrigins` and build the frontend with a browser-reachable
`VITE_API_BASE_URL`.

### 3. Local delivery

- Vite serves local development at port 5173.
- A multi-stage Dockerfile builds static assets and serves them with Nginx.
  `frontend/docker-compose.yml` runs only this frontend on port 5173; PostgreSQL
  and the API are started separately by their owners. This gives parallel
  contributors a repeatable UI container without duplicating R1/R3 deployment
  ownership.
- Do not embed credentials or secrets into frontend build variables. `VITE_*`
  values are public in the resulting browser bundle.

### 4. Verification

- Vitest covers API token propagation, API error mapping, transfer idempotency
  headers, and login error rendering.
- Existing .NET integration tests exercise the real API and PostgreSQL. An R2
  case verifies that an attacker cannot transfer from another client's source
  account and that the rejected operation changes neither balance nor history.
- `npm test` — passed, 4 tests across 2 files.
- `npm run build` — passed, including TypeScript checking and Vite production
  build.
- `npm audit` — passed, 0 vulnerabilities.
- `dotnet test backend/tests/OnlineBanking.IntegrationTests/OnlineBanking.IntegrationTests.csproj -c Release --filter FullyQualifiedName~TransfersTests`
  — passed, 5/5 transfer tests.
- `dotnet test backend/tests/OnlineBanking.IntegrationTests/OnlineBanking.IntegrationTests.csproj -c Release`
  — passed, 13/13 API integration tests.
- `docker compose -f frontend/docker-compose.yml config` — passed.
- `docker compose -f frontend/docker-compose.yml build` — passed.
- `docker compose -f frontend/docker-compose.yml up -d`, followed by
  `Invoke-WebRequest -Uri http://localhost:5173` and
  `docker compose -f frontend/docker-compose.yml down` — passed; HTTP 200 and
  React root confirmed, then container/network stopped.
- `git diff --check` — passed.

## Alternatives considered

- **Server-side rendering / a BFF:** Rejected for Lab 1. The API already exposes
  the required endpoints and the team needs a lightweight independently
  deployable client.
- **Generated OpenAPI client:** Deferred. The contract is small, stable for
  Lab 1, and manually typed interfaces keep the dependency chain short. Any
  API contract changes still need to update the shared OpenAPI file and these
  types together.
- **Store the access token only in memory:** Rejected for this lab's basic
  browser workflow because a page reload would end the session. Local storage
  is an explicit usability trade-off; production hardening should consider an
  HttpOnly secure cookie and stronger XSS controls.
- **Bundle the API and database in the frontend Compose file:** Rejected
  because it duplicates backend and DevOps deployment configuration and makes
  role-parallel local development harder.

## Consequences

- **Positive:** R2 can build and run the client against the documented API
  without waiting for a combined stack, and the same UI is available as a
  Docker container.
- **Positive:** Role separation, error reporting, balance visibility, and
  transfer safety are covered in the UI and API integration checks.
- **Trade-off:** The browser holds a JWT in local storage and API URLs are
  compile-time frontend configuration. These choices suit the lab's local
  monolith but are not a substitute for production security review.
- **Trade-off:** Frontend interfaces are maintained alongside (not generated
  from) the OpenAPI contract; contract changes require coordinated updates.
