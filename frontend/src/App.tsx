import { useEffect, useMemo, useState } from "react";
import type { FormEvent } from "react";
import { api, ApiError, createSession } from "./api";
import type {
  Account,
  Client,
  ClientStatus,
  Currency,
  Session,
  TransactionPage,
} from "./types";

type Page = "overview" | "transfer" | "history" | "accounts" | "admin";
type AuthMode = "login" | "register";

const SESSION_KEY = "online-banking-session";
const PAGE_LABELS: Record<Page, string> = {
  overview: "Огляд",
  transfer: "Переказ",
  history: "Історія",
  accounts: "Рахунки",
  admin: "Клієнти",
};

function readSession(): Session | null {
  const saved = localStorage.getItem(SESSION_KEY);
  if (!saved) return null;

  try {
    const value: unknown = JSON.parse(saved);
    if (
      typeof value === "object" &&
      value !== null &&
      "accessToken" in value &&
      typeof value.accessToken === "string" &&
      "clientId" in value &&
      typeof value.clientId === "string" &&
      "role" in value &&
      (value.role === "Admin" || value.role === "Client") &&
      "expiresInSeconds" in value &&
      typeof value.expiresInSeconds === "number" &&
      "expiresAt" in value &&
      typeof value.expiresAt === "number" &&
      value.expiresAt > Date.now()
    ) {
      return {
        accessToken: value.accessToken,
        clientId: value.clientId,
        role: value.role,
        expiresInSeconds: value.expiresInSeconds,
        expiresAt: value.expiresAt,
      };
    }
  } catch {
    localStorage.removeItem(SESSION_KEY);
    return null;
  }

  localStorage.removeItem(SESSION_KEY);
  return null;
}

function formatMoney(value: number, currency: Currency): string {
  return new Intl.NumberFormat("uk-UA", {
    style: "currency",
    currency,
    minimumFractionDigits: 2,
  }).format(value);
}

function formatDate(value: string): string {
  return new Intl.DateTimeFormat("uk-UA", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value));
}

function errorText(error: unknown, fallback: string): string {
  if (error instanceof Error && error.message) return error.message;
  return fallback;
}

export default function App() {
  const [session, setSession] = useState<Session | null>(readSession);
  const [client, setClient] = useState<Client | null>(null);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [page, setPage] = useState<Page>("overview");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");

  useEffect(() => {
    if (!session) return;
    let active = true;
    setLoading(true);
    Promise.all([api.getMe(session), api.getAccounts(session)])
      .then(([profile, ownAccounts]) => {
        if (!active) return;
        setClient(profile);
        setAccounts(ownAccounts);
        setError("");
      })
      .catch((reason: unknown) => {
        if (!active) return;
        if (reason instanceof ApiError && reason.status === 401) {
          signOut();
          setError("Сеанс завершився. Увійдіть знову.");
          return;
        }
        setError(errorText(reason, "Не вдалося завантажити дані рахунку."));
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [session]);

  function signOut() {
    localStorage.removeItem(SESSION_KEY);
    setSession(null);
    setClient(null);
    setAccounts([]);
    setPage("overview");
    setNotice("");
  }

  function acceptSession(auth: Parameters<typeof createSession>[0]) {
    const nextSession = createSession(auth);
    localStorage.setItem(SESSION_KEY, JSON.stringify(nextSession));
    setError("");
    setNotice("");
    setSession(nextSession);
  }

  async function refreshAccounts() {
    if (!session) return;
    setAccounts(await api.getAccounts(session));
  }

  if (!session) {
    return (
      <AuthScreen
        initialError={error}
        onAuthenticated={acceptSession}
        onError={setError}
      />
    );
  }

  const totalByCurrency = accounts
    .filter((account) => account.status === "Active")
    .reduce<Record<Currency, number>>(
      (totals, account) => {
        totals[account.currency] += account.availableBalance;
        return totals;
      },
      { UAH: 0, USD: 0, EUR: 0 },
    );

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <a className="brand" href="#" onClick={() => setPage("overview")} aria-label="Online Banking — огляд">
          <span className="brand-mark">o</span>
          <span>online<span className="brand-light">bank</span></span>
        </a>
        <div className="sidebar-caption">МЕНЮ</div>
        <nav className="side-nav" aria-label="Головна навігація">
          {(Object.keys(PAGE_LABELS) as Page[])
            .filter((item) => item !== "admin" || session.role === "Admin")
            .map((item) => (
              <button
                className={`nav-item ${page === item ? "active" : ""}`}
                key={item}
                onClick={() => {
                  setPage(item);
                  setError("");
                  setNotice("");
                }}
              >
                <span className="nav-indicator" />
                {PAGE_LABELS[item]}
                {item === "accounts" && <span className="nav-count">{accounts.length}</span>}
              </button>
            ))}
        </nav>
        <div className="sidebar-bottom">
          <div className="secure-note"><span className="secure-dot" /> Захищене з'єднання</div>
          <button className="profile-button" onClick={signOut}>
            <span className="avatar">{client?.fullName.charAt(0).toUpperCase() ?? "U"}</span>
            <span className="profile-copy">
              <strong>{client?.fullName ?? "Мій профіль"}</strong>
              <small>{session.role === "Admin" ? "Адміністратор" : "Клієнт"} · Вийти</small>
            </span>
            <span aria-hidden="true">↗</span>
          </button>
        </div>
      </aside>

      <main className="main-content">
        <header className="topbar">
          <div className="breadcrumb"><span>Особистий кабінет</span><b>/</b>{PAGE_LABELS[page]}</div>
          <div className="topbar-right">
            <span className="live-status"><span /> Система працює</span>
            <span className="topbar-date">{new Intl.DateTimeFormat("uk-UA", { dateStyle: "long" }).format(new Date())}</span>
          </div>
        </header>

        <div className="page-wrap">
          <div className="page-heading">
            <div>
              <p className="eyebrow">{session.role === "Admin" ? "ПАНЕЛЬ АДМІНІСТРАТОРА" : "ВАШІ ФІНАНСИ"}</p>
              <h1>{page === "overview" ? `Вітаємо, ${client?.fullName.split(" ")[0] ?? "користувачу"}` : PAGE_LABELS[page]}</h1>
              <p className="page-subtitle">{pageSubtitle(page)}</p>
            </div>
            <span className="secure-badge">●&nbsp; Безпечний банкінг</span>
          </div>

          {error && <div className="alert alert-error" role="alert">{error}</div>}
          {notice && <div className="alert alert-success" role="status">{notice}</div>}

          {loading && accounts.length === 0 ? (
            <div className="loading-panel"><span className="spinner" /> Завантажуємо дані…</div>
          ) : (
            <>
              {page === "overview" && (
                <Overview
                  accounts={accounts}
                  totals={totalByCurrency}
                  onNavigate={setPage}
                />
              )}
              {page === "accounts" && (
                <Accounts
                  accounts={accounts}
                  session={session}
                  onRefresh={refreshAccounts}
                  onError={setError}
                  onNotice={setNotice}
                />
              )}
              {page === "transfer" && (
                <Transfer
                  accounts={accounts}
                  session={session}
                  onSuccess={async () => {
                    await refreshAccounts();
                    setNotice("Переказ успішно виконано.");
                    setPage("history");
                  }}
                  onError={setError}
                />
              )}
              {page === "history" && (
                <History accounts={accounts} session={session} onError={setError} />
              )}
              {page === "admin" && session.role === "Admin" && (
                <AdminClients session={session} onError={setError} onNotice={setNotice} />
              )}
            </>
          )}
        </div>
      </main>
    </div>
  );
}

function pageSubtitle(page: Page): string {
  const subtitles: Record<Page, string> = {
    overview: "Усі важливі фінансові дані — в одному місці.",
    transfer: "Переказуйте кошти між рахунками онлайн.",
    history: "Переглядайте виконані та відхилені операції.",
    accounts: "Керуйте рахунками та їхніми залишками.",
    admin: "Перегляд клієнтів і керування їхнім доступом.",
  };
  return subtitles[page];
}

function AuthScreen({
  initialError,
  onAuthenticated,
  onError,
}: {
  initialError: string;
  onAuthenticated: (auth: Parameters<typeof createSession>[0]) => void;
  onError: (message: string) => void;
}) {
  const [mode, setMode] = useState<AuthMode>("login");
  const [submitting, setSubmitting] = useState(false);
  const [message, setMessage] = useState(initialError);
  const [form, setForm] = useState({
    fullName: "",
    email: "",
    phone: "",
    birthDate: "",
    taxId: "",
    password: "",
  });

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setMessage("");
    onError("");
    try {
      const auth = mode === "login"
        ? await api.login(form.email, form.password)
        : await api.register(form);
      onAuthenticated(auth);
    } catch (reason) {
      setMessage(errorText(reason, "Не вдалося виконати запит."));
    } finally {
      setSubmitting(false);
    }
  }

  function update(field: keyof typeof form, value: string) {
    setForm((current) => ({ ...current, [field]: value }));
  }

  return (
    <main className="auth-page">
      <div className="auth-aside">
        <a className="brand auth-brand" href="#"><span className="brand-mark">o</span><span>online<span className="brand-light">bank</span></span></a>
        <div className="auth-pitch">
          <p className="eyebrow">ВАШІ ГРОШІ. ВАШІ ПРАВИЛА.</p>
          <h1>Банкінг,<br />що працює<br /><em>для вас.</em></h1>
          <p>Керуйте рахунками та переказуйте кошти — без зайвих кроків і в будь-який час.</p>
        </div>
        <div className="auth-footer">Захищено сучасним шифруванням <span>·</span> © Online Banking</div>
      </div>
      <div className="auth-main">
        <div className="auth-card">
          <div className="auth-tabs" role="tablist" aria-label="Аутентифікація">
            <button type="button" className={mode === "login" ? "selected" : ""} onClick={() => { setMode("login"); setMessage(""); }}>Увійти</button>
            <button type="button" className={mode === "register" ? "selected" : ""} onClick={() => { setMode("register"); setMessage(""); }}>Створити акаунт</button>
          </div>
          <h2>{mode === "login" ? "З поверненням" : "Відкрийте свій рахунок"}</h2>
          <p className="auth-description">{mode === "login" ? "Увійдіть, щоб продовжити роботу з рахунками." : "Заповніть дані, щоб зареєструватися в системі."}</p>
          {message && <div className="alert alert-error" role="alert">{message}</div>}
          <form onSubmit={submit} className="form-stack">
            {mode === "register" && (
              <>
                <label>Повне ім’я<input autoComplete="name" required minLength={2} maxLength={200} value={form.fullName} onChange={(event) => update("fullName", event.target.value)} placeholder="Ім’я Прізвище" /></label>
                <div className="form-row">
                  <label>Телефон<input autoComplete="tel" type="tel" required value={form.phone} onChange={(event) => update("phone", event.target.value)} placeholder="+380671234567" /></label>
                  <label>Дата народження<input type="date" required value={form.birthDate} onChange={(event) => update("birthDate", event.target.value)} /></label>
                </div>
                <label>Ідентифікаційний номер<input required minLength={8} maxLength={20} value={form.taxId} onChange={(event) => update("taxId", event.target.value)} /></label>
              </>
            )}
            <label>Email<input autoComplete="email" type="email" required value={form.email} onChange={(event) => update("email", event.target.value)} placeholder="name@example.com" /></label>
            <label>Пароль<input autoComplete={mode === "login" ? "current-password" : "new-password"} type="password" required minLength={8} value={form.password} onChange={(event) => update("password", event.target.value)} placeholder="Щонайменше 8 символів" /></label>
            {mode === "login" && <div className="auth-hint">Для входу адміністратора використовуйте адміністраторський акаунт.</div>}
            <button className="button button-primary button-wide" disabled={submitting}>
              {submitting ? "Зачекайте…" : mode === "login" ? "Увійти в кабінет" : "Зареєструватися"}
              {!submitting && <span aria-hidden="true">→</span>}
            </button>
          </form>
          <p className="auth-legal">Продовжуючи, ви погоджуєтесь із правилами користування сервісом.</p>
        </div>
      </div>
    </main>
  );
}

function Overview({
  accounts,
  totals,
  onNavigate,
}: {
  accounts: Account[];
  totals: Record<Currency, number>;
  onNavigate: (page: Page) => void;
}) {
  const activeAccounts = accounts.filter((account) => account.status === "Active");
  return (
    <>
      <section className="balance-grid" aria-label="Доступні залишки">
        {(["UAH", "USD", "EUR"] as Currency[]).map((currency, index) => (
          <article className={`balance-card balance-${currency.toLowerCase()}`} key={currency}>
            <div className="balance-card-top"><span>{currencyLabel(currency)}</span><span className="currency-symbol">{["₴", "$", "€"][index]}</span></div>
            <strong>{formatMoney(totals[currency], currency)}</strong>
            <small>{accounts.filter((account) => account.currency === currency && account.status === "Active").length} активних рахунків</small>
          </article>
        ))}
      </section>
      <div className="section-heading">
        <div><h2>Ваші рахунки</h2><p>Короткий огляд активних рахунків</p></div>
        <button className="button button-secondary" onClick={() => onNavigate("accounts")}>Усі рахунки <span>→</span></button>
      </div>
      <AccountCards accounts={activeAccounts.slice(0, 3)} />
      {activeAccounts.length === 0 && <EmptyState title="Рахунків поки немає" description="Створіть рахунок, щоб почати користуватися банкінгом." action="Відкрити рахунок" onClick={() => onNavigate("accounts")} />}
      <section className="quick-transfer">
        <div className="quick-icon">↗</div>
        <div><strong>Потрібно переказати кошти?</strong><p>Швидкий внутрішній переказ за IBAN отримувача.</p></div>
        <button className="button button-primary" onClick={() => onNavigate("transfer")}>Зробити переказ <span>→</span></button>
      </section>
    </>
  );
}

function AccountCards({ accounts }: { accounts: Account[] }) {
  if (accounts.length === 0) return null;
  return (
    <div className="account-grid">
      {accounts.map((account) => (
        <article className="account-card" key={account.id}>
          <div className="account-card-top"><span className={`status-pill status-${account.status.toLowerCase()}`}>{account.status === "Active" ? "Активний" : account.status}</span><span className="account-type">{account.currency} · Поточний</span></div>
          <div className="account-balance">{formatMoney(account.availableBalance, account.currency)}</div>
          <div className="account-iban">{account.iban}</div>
          <div className="account-card-bottom"><span>Доступний баланс</span><span>•••• {account.iban.slice(-4)}</span></div>
        </article>
      ))}
    </div>
  );
}

function Accounts({
  accounts,
  session,
  onRefresh,
  onError,
  onNotice,
}: {
  accounts: Account[];
  session: Session;
  onRefresh: () => Promise<void>;
  onError: (message: string) => void;
  onNotice: (message: string) => void;
}) {
  const [currency, setCurrency] = useState<Currency>("UAH");
  const [busy, setBusy] = useState(false);

  async function openAccount(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    onError("");
    try {
      await api.openAccount(session, currency);
      await onRefresh();
      onNotice(`Відкрито новий рахунок у ${currency}.`);
    } catch (reason) {
      onError(errorText(reason, "Не вдалося відкрити рахунок."));
    } finally {
      setBusy(false);
    }
  }

  async function closeAccount(account: Account) {
    if (!window.confirm(`Закрити рахунок ${account.iban}?`)) return;
    setBusy(true);
    onError("");
    try {
      await api.closeAccount(session, account.id);
      await onRefresh();
      onNotice("Рахунок закрито.");
    } catch (reason) {
      onError(errorText(reason, "Не вдалося закрити рахунок."));
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <section className="panel">
        <div className="section-heading panel-heading"><div><h2>Усі рахунки <span className="count-badge">{accounts.length}</span></h2><p>Баланс, валюта та статус кожного рахунку</p></div></div>
        {accounts.length > 0 ? (
          <div className="table-scroll"><table><thead><tr><th>Рахунок / IBAN</th><th>Валюта</th><th>Баланс</th><th>Статус</th><th>Дія</th></tr></thead>
            <tbody>{accounts.map((account) => <tr key={account.id}><td><strong>{account.iban}</strong><small className="table-secondary">Відкрито {new Intl.DateTimeFormat("uk-UA").format(new Date(account.openedAtUtc))}</small></td><td>{account.currency}</td><td>{formatMoney(account.availableBalance, account.currency)}</td><td><span className={`status-pill status-${account.status.toLowerCase()}`}>{account.status}</span></td><td>{account.status === "Active" && account.balance === 0 && <button className="text-button" disabled={busy} onClick={() => closeAccount(account)}>Закрити</button>}</td></tr>)}</tbody>
          </table></div>
        ) : <p className="empty-inline">Рахунків поки немає. Відкрийте перший нижче.</p>}
      </section>
      <section className="panel open-account-panel">
        <div><h2>Відкрити рахунок</h2><p>Новий рахунок створюється з нульовим балансом.</p></div>
        <form className="open-account-form" onSubmit={openAccount}>
          <label htmlFor="account-currency">Валюта</label>
          <select id="account-currency" value={currency} onChange={(event) => setCurrency(event.target.value as Currency)}><option value="UAH">UAH — гривня</option><option value="USD">USD — долар США</option><option value="EUR">EUR — євро</option></select>
          <button className="button button-primary" disabled={busy}>Відкрити рахунок <span>→</span></button>
        </form>
      </section>
    </>
  );
}

function Transfer({
  accounts,
  session,
  onSuccess,
  onError,
}: {
  accounts: Account[];
  session: Session;
  onSuccess: () => Promise<void>;
  onError: (message: string) => void;
}) {
  const availableAccounts = accounts.filter((account) => account.status === "Active");
  const [fromAccountId, setFromAccountId] = useState(availableAccounts[0]?.id ?? "");
  const [toIban, setToIban] = useState("");
  const [amount, setAmount] = useState("");
  const [description, setDescription] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const selected = availableAccounts.find((account) => account.id === fromAccountId);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    onError("");
    setSubmitting(true);
    try {
      await api.createTransfer(session, {
        fromAccountId,
        toIban: toIban.trim().replace(/\s/g, ""),
        amount: Number(amount),
        ...(description.trim() ? { description: description.trim() } : {}),
      });
      setToIban("");
      setAmount("");
      setDescription("");
      await onSuccess();
    } catch (reason) {
      onError(errorText(reason, "Не вдалося виконати переказ."));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="transfer-layout">
      <section className="panel transfer-panel">
        <div className="panel-heading"><p className="eyebrow">ВНУТРІШНІЙ ПЕРЕКАЗ</p><h2>Новий переказ</h2><p>Кошти зараховуються отримувачу після підтвердження операції.</p></div>
        {availableAccounts.length === 0 ? <EmptyState title="Немає активного рахунку" description="Відкрийте рахунок, щоб здійснювати перекази." /> : (
          <form className="form-stack transfer-form" onSubmit={submit}>
            <label>З рахунку<select required value={fromAccountId} onChange={(event) => setFromAccountId(event.target.value)}>{availableAccounts.map((account) => <option value={account.id} key={account.id}>{account.iban} · {formatMoney(account.availableBalance, account.currency)}</option>)}</select></label>
            <label>IBAN отримувача<input required value={toIban} onChange={(event) => setToIban(event.target.value)} placeholder="UA00 0000 0000 0000 0000 0000 000" autoComplete="off" /></label>
            <div className="form-row">
              <label>Сума<input required type="number" min="0.01" step="0.01" max={selected?.availableBalance} value={amount} onChange={(event) => setAmount(event.target.value)} placeholder="0.00" /></label>
              <label>Валюта<select value={selected?.currency ?? "UAH"} disabled><option>{selected?.currency ?? "UAH"}</option></select></label>
            </div>
            <label>Призначення платежу <span className="optional">(необов’язково)</span><input maxLength={250} value={description} onChange={(event) => setDescription(event.target.value)} placeholder="Наприклад, оплата рахунку" /></label>
            <div className="transfer-available"><span>Доступно для переказу</span><strong>{selected ? formatMoney(selected.availableBalance, selected.currency) : "—"}</strong></div>
            <button className="button button-primary button-wide" disabled={submitting || !selected}>{submitting ? "Виконуємо переказ…" : "Підтвердити переказ"} {!submitting && <span>→</span>}</button>
          </form>
        )}
      </section>
      <aside className="transfer-info">
        <div className="info-icon">i</div><h3>Перед підтвердженням</h3>
        <ul><li>Переконайтеся, що IBAN отримувача введено правильно.</li><li>Переказ можливий між активними рахунками в одній валюті.</li><li>Операції з недостатнім балансом буде відхилено.</li></ul>
        <div className="info-note">Кожен запит переказу захищено ключем ідемпотентності, щоб уникнути повторного списання.</div>
      </aside>
    </div>
  );
}

function History({
  accounts,
  session,
  onError,
}: {
  accounts: Account[];
  session: Session;
  onError: (message: string) => void;
}) {
  const [accountId, setAccountId] = useState("");
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<TransactionPage | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let active = true;
    setLoading(true);
    api.getTransactions(session, accountId || null, page)
      .then((value) => { if (active) setResult(value); })
      .catch((reason: unknown) => { if (active) onError(errorText(reason, "Не вдалося завантажити історію.")); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [accountId, page, session, onError]);

  const pageCount = Math.max(1, Math.ceil((result?.totalCount ?? 0) / (result?.pageSize ?? 10)));
  return (
    <section className="panel">
      <div className="section-heading panel-heading">
        <div><h2>Операції</h2><p>Показано {result?.items.length ?? 0} з {result?.totalCount ?? 0} операцій</p></div>
        <label className="filter-label">Рахунок<select value={accountId} onChange={(event) => { setAccountId(event.target.value); setPage(1); }}><option value="">Усі рахунки</option>{accounts.map((account) => <option value={account.id} key={account.id}>{account.iban} · {account.currency}</option>)}</select></label>
      </div>
      {loading ? <div className="loading-panel"><span className="spinner" /> Завантажуємо історію…</div> : !result?.items.length ? <EmptyState title="Операцій поки немає" description="Історія переказів з’явиться після першої операції." /> : (
        <>
          <div className="table-scroll"><table><thead><tr><th>Операція</th><th>Дата</th><th>IBAN отримувача</th><th>Сума</th><th>Статус</th></tr></thead><tbody>
            {result.items.map((tx) => {
              const outgoing = accounts.some((account) => account.id === tx.fromAccountId);
              return <tr key={tx.id}><td><strong>{tx.description || "Внутрішній переказ"}</strong><small className="table-secondary">{outgoing ? "Відправлено" : "Отримано"}</small></td><td>{formatDate(tx.createdAtUtc)}</td><td>{tx.toIban}</td><td className={outgoing ? "amount-out" : "amount-in"}>{outgoing ? "−" : "+"}{formatMoney(tx.amount, tx.currency)}</td><td><span className={`status-pill status-${tx.status.toLowerCase()}`}>{transactionStatus(tx.status)}</span></td></tr>;
            })}
          </tbody></table></div>
          <div className="pagination"><span>Сторінка {result.page} із {pageCount}</span><div><button className="button button-secondary" disabled={page <= 1} onClick={() => setPage((current) => current - 1)}>← Назад</button><button className="button button-secondary" disabled={page >= pageCount} onClick={() => setPage((current) => current + 1)}>Далі →</button></div></div>
        </>
      )}
    </section>
  );
}

function AdminClients({
  session,
  onError,
  onNotice,
}: {
  session: Session;
  onError: (message: string) => void;
  onNotice: (message: string) => void;
}) {
  const [clients, setClients] = useState<Client[]>([]);
  const [loading, setLoading] = useState(true);
  const [pendingId, setPendingId] = useState("");
  const [filter, setFilter] = useState("");

  async function loadClients() {
    setLoading(true);
    onError("");
    try {
      setClients(await api.getClients(session));
    } catch (reason) {
      onError(errorText(reason, "Не вдалося завантажити список клієнтів."));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void loadClients();
  }, [session]);

  async function changeStatus(client: Client) {
    const status: ClientStatus = client.status === "Active" ? "Blocked" : "Active";
    setPendingId(client.id);
    onError("");
    try {
      const updated = await api.updateClientStatus(session, client.id, status);
      setClients((current) => current.map((item) => item.id === updated.id ? updated : item));
      onNotice(`Статус клієнта змінено на «${updated.status}».`);
    } catch (reason) {
      onError(errorText(reason, "Не вдалося змінити статус клієнта."));
    } finally {
      setPendingId("");
    }
  }

  const visibleClients = useMemo(() => {
    const query = filter.trim().toLocaleLowerCase("uk-UA");
    return clients.filter((item) => !query || `${item.fullName} ${item.email} ${item.taxId}`.toLocaleLowerCase("uk-UA").includes(query));
  }, [clients, filter]);

  return (
    <section className="panel">
      <div className="section-heading panel-heading"><div><h2>Клієнти <span className="count-badge">{clients.length}</span></h2><p>Адміністрування доступу користувачів</p></div><label className="search-label"><span>⌕</span><input aria-label="Пошук клієнта" value={filter} onChange={(event) => setFilter(event.target.value)} placeholder="Ім’я, email або ІПН" /></label></div>
      {loading ? <div className="loading-panel"><span className="spinner" /> Завантажуємо список…</div> : <div className="table-scroll"><table><thead><tr><th>Клієнт</th><th>Email</th><th>Роль</th><th>Статус</th><th>Дія</th></tr></thead><tbody>
        {visibleClients.map((item) => <tr key={item.id}><td><strong>{item.fullName}</strong><small className="table-secondary">ІПН: {item.taxId}</small></td><td>{item.email}</td><td>{item.role === "Admin" ? "Адміністратор" : "Клієнт"}</td><td><span className={`status-pill status-${item.status.toLowerCase()}`}>{item.status === "Active" ? "Активний" : "Заблокований"}</span></td><td><button className={`text-button ${item.status === "Active" ? "danger-text" : ""}`} disabled={pendingId === item.id} onClick={() => changeStatus(item)}>{pendingId === item.id ? "Зачекайте…" : item.status === "Active" ? "Заблокувати" : "Розблокувати"}</button></td></tr>)}
      </tbody></table>{visibleClients.length === 0 && <p className="empty-inline">За цим запитом клієнтів не знайдено.</p>}</div>}
    </section>
  );
}

function EmptyState({
  title,
  description,
  action,
  onClick,
}: {
  title: string;
  description: string;
  action?: string;
  onClick?: () => void;
}) {
  return <div className="empty-state"><span className="empty-icon">○</span><h3>{title}</h3><p>{description}</p>{action && onClick && <button className="button button-primary" onClick={onClick}>{action}</button>}</div>;
}

function currencyLabel(currency: Currency): string {
  const labels: Record<Currency, string> = { UAH: "Українська гривня", USD: "Долар США", EUR: "Євро" };
  return labels[currency];
}

function transactionStatus(status: string): string {
  const labels: Record<string, string> = { Completed: "Виконано", Pending: "В обробці", Failed: "Відхилено" };
  return labels[status] ?? status;
}
