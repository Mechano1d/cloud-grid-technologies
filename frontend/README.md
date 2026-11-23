# Online Banking — user guide (R2)

React + TypeScript web client for the Lab 1 API described in
[`docs/openapi/lab1-monolith.yaml`](../docs/openapi/lab1-monolith.yaml).

## Developer setup

### Requirements

- Node.js 22 or newer and npm.
- The Lab 1 API running at `http://localhost:8080`.
- For API integration tests: .NET 10 SDK and Docker with the Docker Engine running.

Start PostgreSQL and the API first. For a local PowerShell setup, run the
following in terminal A (skip the database command if PostgreSQL is already
running):

```powershell
docker run -d --name banking-postgres `
  -e POSTGRES_DB=online_banking `
  -e POSTGRES_USER=banking_user `
  -e POSTGRES_PASSWORD=change_me_local_password `
  -p 5432:5432 postgres:17-alpine

$env:ConnectionStrings__Default = "Host=localhost;Port=5432;Database=online_banking;Username=banking_user;Password=change_me_local_password"
$env:Jwt__SecretKey = "replace_with_at_least_32_char_secret_key_for_local_dev"
dotnet run --project backend/src/Monolith/OnlineBanking.Api --environment Development --urls http://localhost:8080
```

In terminal B, run the frontend:

```powershell
cd frontend
npm ci
npm run dev
```

Open `http://localhost:5173`. The default API URL is `http://localhost:8080`.
To use another API URL, set `VITE_API_BASE_URL` before starting Vite or building
the Docker image. The API must allow the frontend's origin in `Cors:AllowedOrigins`.
`frontend/.env.example` documents the local setting.

### Checks

```powershell
cd frontend
npm test
npm run build
```

The frontend tests cover bearer-token attachment, RFC 7807 error display,
transfer idempotency headers, and rejected login credentials. API integration
tests run against PostgreSQL Testcontainers:

```powershell
dotnet test backend/tests/OnlineBanking.IntegrationTests/OnlineBanking.IntegrationTests.csproj `
  -c Release --filter FullyQualifiedName~TransfersTests
```

The API tests cover successful transfers, insufficient funds, a source account
owned by another client, currency/account status conflicts, idempotency replay,
history access, and concurrent transfers. The full backend test suite is
documented in the [root README](../README.md).

### Docker UI

Start the API and PostgreSQL first. From the repository root, build and run the
frontend container:

```powershell
docker compose -f frontend/docker-compose.yml up --build
```

Open `http://localhost:5173`. Stop it with `Ctrl+C`, or run
`docker compose -f frontend/docker-compose.yml down` in another terminal.
This Compose file runs only the static web client; it deliberately does not
replace the backend or database setup. The browser calls the API directly, so
the API URL must be reachable from the developer's browser and its CORS policy
must allow `http://localhost:5173`.

For a different API URL:

```powershell
$env:VITE_API_BASE_URL = "https://api.example.test"
docker compose -f frontend/docker-compose.yml up --build
```

The variable is embedded at image build time. Do not put credentials or
environment-specific secrets in `VITE_*` variables.

## User guide

1. Open the web client and sign in with your registered email and password, or
   choose **Створити акаунт** to register. All registration fields are required.
2. The **Огляд** page shows available balances grouped by currency and active
   accounts. Go to **Рахунки** to open a UAH, USD, or EUR account. New accounts
   have a zero balance.
3. Choose **Переказ**, select an active source account, enter the recipient IBAN
   and a positive amount, and confirm. The transfer must be covered by the
   available balance and the destination must be active and use the same
   currency. A server rejection (for example insufficient funds or an unknown
   IBAN) is shown in the page; the balance is not optimistically changed.
4. Open **Історія** to review operations and filter them by account. Use the
   page controls to view older operations.
5. A zero-balance active account can be closed from **Рахунки**. Account history
   remains available according to API ownership rules.
6. Administrators see an additional **Клієнти** page where they can search
   clients and activate or block an account. Role-based access is enforced by
   the API; hiding this page in the UI is not a security boundary.
7. Use **Вийти** by clicking the profile area in the sidebar. The JWT is kept in
   browser local storage until logout or token expiration; avoid shared devices.

For development only, the backend seeds demo accounts when started in the
Development environment. The demo credentials are listed in
`backend/src/Monolith/OnlineBanking.Persistence/Seeding/DevDataSeeder.cs`.
Do not use these credentials or seeded data in a deployed environment.
