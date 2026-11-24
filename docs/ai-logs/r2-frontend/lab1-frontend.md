# R2 — Lab 1 Frontend AI Usage Log

- **Work:** React/TypeScript client, API tests, Docker UI setup, and
  R2 documentation.
- **Assistant:** Copilot SDK in VS Code.
- **Tools used:** Workspace file/search tools, PowerShell for package and test
  commands, and patch-based source editing.
- **Delegated work:** None.
- **Prompt summary:** Implement the frontend-client against
  `docs/openapi/lab1-monolith.yaml`, support JWT client/admin flows, add
  positive/negative verification for banking operations, document local use,
  and prepare architecture and pull-request details.
- **Decisions:** Call the existing API directly; keep role authorization
  server-side; persist the lab JWT only until logout/expiry; include a
  per-request transfer idempotency key; keep the Docker Compose scope to the
  frontend so backend/DevOps configuration is not duplicated.
- **Verification:** `npm test` (4 passed), `npm run build` (passed),
  `npm audit` (0 vulnerabilities), targeted API transfer integration tests
  (5 passed), full API integration suite (13 passed), Docker Compose config and
  image build (passed), and container HTTP smoke test (200). Results and
  instructions are recorded in `docs/architecture/002-frontend-architecture.md`
  and `frontend/README.md`. No credentials or private local paths are recorded.
