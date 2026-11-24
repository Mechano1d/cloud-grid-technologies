export type UserRole = "Client" | "Admin";
export type AccountStatus = "Active" | "Frozen" | "Closed";
export type Currency = "UAH" | "USD" | "EUR";
export type ClientStatus = "Active" | "Blocked";

export interface AuthResponse {
  accessToken: string;
  expiresInSeconds: number;
  clientId: string;
  role: UserRole;
}

export interface Session extends AuthResponse {
  expiresAt: number;
}

export interface Client {
  id: string;
  fullName: string;
  email: string;
  phone: string;
  birthDate: string;
  taxId: string;
  role: UserRole;
  status: ClientStatus;
  createdAtUtc: string;
}

export interface Account {
  id: string;
  clientId: string;
  iban: string;
  currency: Currency;
  balance: number;
  blockedAmount: number;
  availableBalance: number;
  status: AccountStatus;
  openedAtUtc: string;
}

export interface Transaction {
  id: string;
  fromAccountId: string;
  toAccountId: string;
  toIban: string;
  amount: number;
  currency: Currency;
  status: "Pending" | "Completed" | "Failed";
  type: "InternalTransfer";
  description: string | null;
  idempotencyKey: string | null;
  createdAtUtc: string;
}

export interface TransactionPage {
  items: Transaction[];
  page: number;
  pageSize: number;
  totalCount: number;
}
