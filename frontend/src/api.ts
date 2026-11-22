import type {
  Account,
  AuthResponse,
  Client,
  ClientStatus,
  Currency,
  Session,
  Transaction,
  TransactionPage,
} from "./types";

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? "http://localhost:8080").replace(/\/$/, "");

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly code?: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

async function request<T>(
  path: string,
  options: RequestInit = {},
  session?: Session | null,
): Promise<T> {
  const headers = new Headers(options.headers);
  headers.set("Accept", "application/json");
  if (options.body !== undefined) {
    headers.set("Content-Type", "application/json");
  }
  if (session?.accessToken) {
    headers.set("Authorization", `Bearer ${session.accessToken}`);
  }

  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl}${path}`, { ...options, headers });
  } catch (error) {
    if (error instanceof TypeError) {
      throw new Error("Не вдалося з'єднатися з сервером. Перевірте, чи API запущено.");
    }
    throw error;
  }

  if (!response.ok) {
    const problem: unknown = await response.json().catch(() => null);
    const details = isRecord(problem) ? problem : undefined;
    const message =
      (typeof details?.detail === "string" && details.detail) ||
      (typeof details?.title === "string" && details.title) ||
      `Помилка запиту (${response.status}).`;
    const code = typeof details?.code === "string" ? details.code : undefined;
    throw new ApiError(message, response.status, code);
  }

  return (await response.json()) as T;
}

export const api = {
  login(email: string, password: string) {
    return request<AuthResponse>("/api/v1/auth/login", {
      method: "POST",
      body: JSON.stringify({ email, password }),
    });
  },

  register(input: {
    fullName: string;
    email: string;
    phone: string;
    birthDate: string;
    taxId: string;
    password: string;
  }) {
    return request<AuthResponse>("/api/v1/auth/register", {
      method: "POST",
      body: JSON.stringify(input),
    });
  },

  getMe(session: Session) {
    return request<Client>("/api/v1/clients/me", {}, session);
  },

  getAccounts(session: Session) {
    return request<Account[]>("/api/v1/accounts", {}, session);
  },

  openAccount(session: Session, currency: Currency) {
    return request<Account>(
      "/api/v1/accounts",
      { method: "POST", body: JSON.stringify({ currency, initialDeposit: 0 }) },
      session,
    );
  },

  closeAccount(session: Session, accountId: string) {
    return request<Account>(`/api/v1/accounts/${accountId}/close`, { method: "POST" }, session);
  },

  createTransfer(
    session: Session,
    input: { fromAccountId: string; toIban: string; amount: number; description?: string },
  ) {
    return request<Transaction>(
      "/api/v1/transactions/transfers",
      {
        method: "POST",
        headers: { "Idempotency-Key": crypto.randomUUID() },
        body: JSON.stringify(input),
      },
      session,
    );
  },

  getTransactions(session: Session, accountId: string | null, page: number) {
    const query = new URLSearchParams({ page: String(page), pageSize: "10" });
    if (accountId) query.set("accountId", accountId);
    return request<TransactionPage>(`/api/v1/transactions?${query}`, {}, session);
  },

  getClients(session: Session) {
    return request<Client[]>("/api/v1/clients", {}, session);
  },

  updateClientStatus(session: Session, clientId: string, status: ClientStatus) {
    return request<Client>(
      `/api/v1/clients/${clientId}/status`,
      { method: "PATCH", body: JSON.stringify({ status }) },
      session,
    );
  },
};

export function createSession(auth: AuthResponse): Session {
  return { ...auth, expiresAt: Date.now() + auth.expiresInSeconds * 1000 };
}
