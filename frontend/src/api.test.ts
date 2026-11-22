import { afterEach, describe, expect, it, vi } from "vitest";
import { api, createSession } from "./api";

afterEach(() => {
  vi.restoreAllMocks();
});

describe("API client", () => {
  it("adds the bearer token to authenticated requests", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response(JSON.stringify([]), { status: 200 }),
    );
    const session = createSession({
      accessToken: "test-token",
      expiresInSeconds: 3600,
      clientId: "client-id",
      role: "Client",
    });

    await api.getAccounts(session);

    expect(fetchMock).toHaveBeenCalledWith(expect.stringContaining("/api/v1/accounts"), expect.any(Object));
    const headers = new Headers(fetchMock.mock.calls[0]?.[1]?.headers);
    expect(headers.get("Authorization")).toBe("Bearer test-token");
  });

  it("surfaces API problem details and status for failed requests", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response(
        JSON.stringify({ status: 409, detail: "Insufficient funds", code: "Accounts.InsufficientFunds" }),
        { status: 409, headers: { "Content-Type": "application/problem+json" } },
      ),
    );

    await expect(api.login("user@example.com", "password")).rejects.toMatchObject({
      message: "Insufficient funds",
      status: 409,
      code: "Accounts.InsufficientFunds",
    });
  });

  it("sends a fresh idempotency key with each transfer", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response(JSON.stringify({ id: "transfer-id" }), { status: 201 }),
    );
    const session = createSession({
      accessToken: "test-token",
      expiresInSeconds: 3600,
      clientId: "client-id",
      role: "Client",
    });

    await api.createTransfer(session, {
      fromAccountId: "account-id",
      toIban: "UA213223130000026007233566001",
      amount: 10,
    });

    const headers = new Headers(fetchMock.mock.calls[0]?.[1]?.headers);
    expect(headers.get("Idempotency-Key")).toMatch(
      /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i,
    );
  });
});
