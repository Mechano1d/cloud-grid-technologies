CREATE TABLE IF NOT EXISTS public."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId" = '20261121090515_InitialCreate') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'accounts') THEN
            CREATE SCHEMA accounts;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId" = '20261121090515_InitialCreate') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'clients') THEN
            CREATE SCHEMA clients;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId" = '20261121090515_InitialCreate') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'transactions') THEN
            CREATE SCHEMA transactions;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId" = '20261121090515_InitialCreate') THEN
    CREATE TABLE accounts.accounts (
        id uuid NOT NULL,
        client_id uuid NOT NULL,
        iban character varying(34) NOT NULL,
        currency character varying(16) NOT NULL,
        balance numeric(18,2) NOT NULL,
        blocked_amount numeric(18,2) NOT NULL,
        status character varying(16) NOT NULL,
        opened_at_utc timestamp with time zone NOT NULL,
        CONSTRAINT "PK_accounts" PRIMARY KEY (id),
        CONSTRAINT ck_accounts_balance_non_negative CHECK (balance >= 0),
        CONSTRAINT ck_accounts_blocked_non_negative CHECK (blocked_amount >= 0)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId" = '20261121090515_InitialCreate') THEN
    CREATE TABLE clients.clients (
        id uuid NOT NULL,
        full_name character varying(200) NOT NULL,
        email character varying(256) NOT NULL,
        phone character varying(30) NOT NULL,
        birth_date date NOT NULL,
        tax_id character varying(20) NOT NULL,
        password_hash character varying(512) NOT NULL,
        role character varying(32) NOT NULL,
        status character varying(32) NOT NULL,
        status_reason character varying(500),
        created_at_utc timestamp with time zone NOT NULL,
        CONSTRAINT "PK_clients" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId" = '20261121090515_InitialCreate') THEN
    CREATE TABLE transactions.transactions (
        id uuid NOT NULL,
        initiated_by_client_id uuid NOT NULL,
        from_account_id uuid NOT NULL,
        to_account_id uuid NOT NULL,
        to_iban character varying(34) NOT NULL,
        amount numeric(18,2) NOT NULL,
        currency character varying(16) NOT NULL,
        status character varying(32) NOT NULL,
        type character varying(32) NOT NULL,
        description character varying(250),
        idempotency_key character varying(100),
        failure_reason character varying(500),
        created_at_utc timestamp with time zone NOT NULL,
        completed_at_utc timestamp with time zone,
        CONSTRAINT "PK_transactions" PRIMARY KEY (id),
        CONSTRAINT ck_transactions_amount_positive CHECK (amount > 0)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId" = '20261121090515_InitialCreate') THEN
    CREATE INDEX ix_accounts_client_id ON accounts.accounts (client_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId" = '20261121090515_InitialCreate') THEN
    CREATE UNIQUE INDEX ux_accounts_iban ON accounts.accounts (iban);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId" = '20261121090515_InitialCreate') THEN
    CREATE UNIQUE INDEX ux_clients_email ON clients.clients (email);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId" = '20261121090515_InitialCreate') THEN
    CREATE UNIQUE INDEX ux_clients_tax_id ON clients.clients (tax_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId" = '20261121090515_InitialCreate') THEN
    CREATE INDEX ix_transactions_created_at_utc ON transactions.transactions (created_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId" = '20261121090515_InitialCreate') THEN
    CREATE INDEX ix_transactions_from_account_id ON transactions.transactions (from_account_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId" = '20261121090515_InitialCreate') THEN
    CREATE INDEX ix_transactions_to_account_id ON transactions.transactions (to_account_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId" = '20261121090515_InitialCreate') THEN
    CREATE UNIQUE INDEX ux_transactions_client_idempotency_key ON transactions.transactions (initiated_by_client_id, idempotency_key) WHERE idempotency_key IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId" = '20261121090515_InitialCreate') THEN
    INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261121090515_InitialCreate', '10.0.15');
    END IF;
END $EF$;
COMMIT;

