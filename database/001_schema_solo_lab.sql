-- Gestionale Sciuscia - schema fisico SQLite Solo LAB
-- Versione 1.0 - 2026-09-12
-- Tutte le date/ore sono ISO-8601; gli importi sono espressi in centesimi di euro.

PRAGMA foreign_keys = ON;
PRAGMA journal_mode = WAL;
PRAGMA synchronous = FULL;
PRAGMA busy_timeout = 5000;

BEGIN IMMEDIATE;

CREATE TABLE schema_migrations (
    version             INTEGER PRIMARY KEY,
    name                TEXT NOT NULL,
    applied_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
) STRICT;

CREATE TABLE sites (
    site_id             TEXT PRIMARY KEY CHECK (length(site_id) = 36),
    code                TEXT NOT NULL UNIQUE CHECK (code <> ''),
    name                TEXT NOT NULL CHECK (name <> ''),
    active              INTEGER NOT NULL DEFAULT 1 CHECK (active IN (0,1)),
    created_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    updated_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    row_version         INTEGER NOT NULL DEFAULT 1 CHECK (row_version >= 1)
) STRICT;

CREATE TABLE operators (
    operator_id         TEXT PRIMARY KEY CHECK (length(operator_id) = 36),
    display_name        TEXT NOT NULL CHECK (display_name <> ''),
    active              INTEGER NOT NULL DEFAULT 1 CHECK (active IN (0,1)),
    created_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    updated_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    row_version         INTEGER NOT NULL DEFAULT 1 CHECK (row_version >= 1)
) STRICT;

CREATE TABLE installation_settings (
    settings_id         INTEGER PRIMARY KEY CHECK (settings_id = 1),
    site_id             TEXT NOT NULL REFERENCES sites(site_id),
    store_name          TEXT NOT NULL,
    store_address       TEXT,
    store_phone_1       TEXT,
    store_phone_2       TEXT,
    opening_hours       TEXT,
    receipt_warning     TEXT NOT NULL,
    photos_root         TEXT NOT NULL,
    backup_root         TEXT NOT NULL,
    automatic_backup_at TEXT NOT NULL DEFAULT '14:00' CHECK (automatic_backup_at GLOB '[0-2][0-9]:[0-5][0-9]'),
    locale              TEXT NOT NULL DEFAULT 'it-IT',
    -- Capacità di lavoro interna di una giornata intera (minuti), usata per confrontare il
    -- carico previsto (somma dei tempi delle lavorazioni) con quanto è realisticamente
    -- gestibile. Le mezze giornate dimezzano questo valore (vedi WorkCalendarService).
    daily_capacity_minutes INTEGER NOT NULL DEFAULT 480 CHECK (daily_capacity_minutes > 0),
    updated_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    row_version         INTEGER NOT NULL DEFAULT 1 CHECK (row_version >= 1)
) STRICT;

CREATE TABLE printer_profiles (
    printer_profile_id  TEXT PRIMARY KEY CHECK (length(printer_profile_id) = 36),
    purpose             TEXT NOT NULL CHECK (purpose IN ('RICEVUTE','FOTOGRAFIE','ETICHETTE')),
    display_name        TEXT NOT NULL,
    windows_printer     TEXT NOT NULL,
    paper_width_mm      REAL,
    paper_height_mm     REAL,
    dpi                 INTEGER CHECK (dpi IS NULL OR dpi > 0),
    configuration_json  TEXT CHECK (configuration_json IS NULL OR json_valid(configuration_json)),
    is_default          INTEGER NOT NULL DEFAULT 0 CHECK (is_default IN (0,1)),
    active              INTEGER NOT NULL DEFAULT 1 CHECK (active IN (0,1)),
    created_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    updated_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    row_version         INTEGER NOT NULL DEFAULT 1 CHECK (row_version >= 1)
) STRICT;

CREATE UNIQUE INDEX ux_printer_default_purpose
ON printer_profiles(purpose) WHERE is_default = 1 AND active = 1;

CREATE TABLE number_sequences (
    site_id             TEXT NOT NULL REFERENCES sites(site_id),
    sequence_name       TEXT NOT NULL,
    sequence_year       INTEGER NOT NULL CHECK (sequence_year BETWEEN 2000 AND 9999),
    last_value          INTEGER NOT NULL DEFAULT 0 CHECK (last_value >= 0),
    updated_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    PRIMARY KEY (site_id, sequence_name, sequence_year)
) STRICT;

CREATE TABLE customers (
    customer_id         TEXT PRIMARY KEY CHECK (length(customer_id) = 36),
    surname             TEXT NOT NULL CHECK (trim(surname) <> ''),
    first_name          TEXT,
    mobile_original     TEXT,
    mobile_normalized   TEXT,
    email               TEXT,
    privacy_status      TEXT NOT NULL DEFAULT 'NON_REGISTRATA'
                        CHECK (privacy_status IN ('NON_REGISTRATA','ACCONSENTE','NON_ACCONSENTE')),
    privacy_recorded_at TEXT,
    notes               TEXT,
    active              INTEGER NOT NULL DEFAULT 1 CHECK (active IN (0,1)),
    created_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    updated_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    row_version         INTEGER NOT NULL DEFAULT 1 CHECK (row_version >= 1)
) STRICT;

CREATE UNIQUE INDEX ux_customers_mobile
ON customers(mobile_normalized) WHERE mobile_normalized IS NOT NULL AND mobile_normalized <> '';
CREATE INDEX ix_customers_name ON customers(surname, first_name);

CREATE TABLE categories (
    category_id         TEXT PRIMARY KEY CHECK (length(category_id) = 36),
    name                TEXT NOT NULL COLLATE NOCASE UNIQUE CHECK (trim(name) <> ''),
    sort_order          INTEGER NOT NULL DEFAULT 0,
    active              INTEGER NOT NULL DEFAULT 1 CHECK (active IN (0,1)),
    created_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    updated_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    row_version         INTEGER NOT NULL DEFAULT 1 CHECK (row_version >= 1)
) STRICT;

CREATE TABLE brands (
    brand_id            TEXT PRIMARY KEY CHECK (length(brand_id) = 36),
    canonical_name      TEXT NOT NULL COLLATE NOCASE UNIQUE CHECK (trim(canonical_name) <> ''),
    protected           INTEGER NOT NULL DEFAULT 0 CHECK (protected IN (0,1)),
    active              INTEGER NOT NULL DEFAULT 1 CHECK (active IN (0,1)),
    created_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    updated_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    row_version         INTEGER NOT NULL DEFAULT 1 CHECK (row_version >= 1)
) STRICT;

CREATE TABLE brand_merges (
    brand_merge_id      TEXT PRIMARY KEY CHECK (length(brand_merge_id) = 36),
    removed_brand_id    TEXT NOT NULL REFERENCES brands(brand_id),
    retained_brand_id   TEXT NOT NULL REFERENCES brands(brand_id),
    merged_at           TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    reversed_at         TEXT,
    operator_id         TEXT REFERENCES operators(operator_id),
    CHECK (removed_brand_id <> retained_brand_id)
) STRICT;

CREATE TABLE artisans (
    artisan_id          TEXT PRIMARY KEY CHECK (length(artisan_id) = 36),
    display_name        TEXT NOT NULL CHECK (trim(display_name) <> ''),
    contact_name        TEXT,
    phone               TEXT,
    email               TEXT,
    address             TEXT,
    notes               TEXT,
    active              INTEGER NOT NULL DEFAULT 1 CHECK (active IN (0,1)),
    created_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    updated_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    row_version         INTEGER NOT NULL DEFAULT 1 CHECK (row_version >= 1)
) STRICT;

CREATE TABLE work_types (
    work_type_id        TEXT PRIMARY KEY CHECK (length(work_type_id) = 36),
    name                TEXT NOT NULL COLLATE NOCASE UNIQUE CHECK (trim(name) <> ''),
    label_text          TEXT NOT NULL CHECK (trim(label_text) <> ''),
    execution_default   TEXT NOT NULL DEFAULT 'INTERNA'
                        CHECK (execution_default IN ('INTERNA','ESTERNA')),
    price_mode          TEXT NOT NULL DEFAULT 'FISSO'
                        CHECK (price_mode IN ('FISSO','FASCE','DA_DEFINIRE')),
    standard_price_cents INTEGER CHECK (standard_price_cents IS NULL OR standard_price_cents >= 0),
    standard_cost_cents INTEGER CHECK (standard_cost_cents IS NULL OR standard_cost_cents >= 0),
    -- Tempo stimato per completare la lavorazione (minuti), usato per calcolare il carico
    -- giornaliero in Agenda lavori/Consulta carico. Assente per le lavorazioni a testo libero.
    default_duration_minutes INTEGER CHECK (default_duration_minutes IS NULL OR default_duration_minutes > 0),
    sort_order          INTEGER NOT NULL DEFAULT 0,
    active              INTEGER NOT NULL DEFAULT 1 CHECK (active IN (0,1)),
    created_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    updated_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    row_version         INTEGER NOT NULL DEFAULT 1 CHECK (row_version >= 1)
) STRICT;

CREATE TABLE work_type_price_bands (
    work_type_id        TEXT NOT NULL REFERENCES work_types(work_type_id),
    band_code           TEXT NOT NULL CHECK (band_code IN ('A','B','C')),
    price_cents         INTEGER NOT NULL CHECK (price_cents >= 0),
    cost_cents          INTEGER CHECK (cost_cents IS NULL OR cost_cents >= 0),
    PRIMARY KEY (work_type_id, band_code)
) STRICT;

CREATE TABLE category_work_compatibility (
    category_id         TEXT NOT NULL REFERENCES categories(category_id),
    work_type_id        TEXT NOT NULL REFERENCES work_types(work_type_id),
    allowed             INTEGER NOT NULL DEFAULT 1 CHECK (allowed IN (0,1)),
    warning_text        TEXT,
    PRIMARY KEY (category_id, work_type_id)
) STRICT;

CREATE TABLE orders (
    order_id            TEXT PRIMARY KEY CHECK (length(order_id) = 36),
    site_id             TEXT NOT NULL REFERENCES sites(site_id),
    customer_id         TEXT NOT NULL REFERENCES customers(customer_id),
    order_year          INTEGER NOT NULL CHECK (order_year BETWEEN 2000 AND 9999),
    order_number        INTEGER NOT NULL CHECK (order_number > 0),
    visible_code        TEXT NOT NULL UNIQUE CHECK (trim(visible_code) <> ''),
    received_at         TEXT NOT NULL,
    status              TEXT NOT NULL DEFAULT 'APERTA'
                        CHECK (status IN ('APERTA','CHIUSA','ANNULLATA')),
    notes               TEXT,
    canceled_at         TEXT,
    cancellation_reason TEXT,
    resolution_status   TEXT NOT NULL DEFAULT 'NON_NECESSARIA'
                        CHECK (resolution_status IN ('NON_NECESSARIA','DA_RISOLVERE','RISOLTA')),
    resolved_at         TEXT,
    closed_at           TEXT,
    reopened_at         TEXT,
    reopen_reason       TEXT,
    created_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    updated_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    row_version         INTEGER NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    UNIQUE (site_id, order_year, order_number),
    CHECK ((status <> 'ANNULLATA') OR (canceled_at IS NOT NULL AND cancellation_reason IS NOT NULL)),
    CHECK ((resolution_status <> 'RISOLTA') OR resolved_at IS NOT NULL)
) STRICT;

CREATE INDEX ix_orders_customer ON orders(customer_id, received_at DESC);
CREATE INDEX ix_orders_status ON orders(status, received_at DESC);

CREATE TABLE items (
    item_id             TEXT PRIMARY KEY CHECK (length(item_id) = 36),
    order_id            TEXT NOT NULL REFERENCES orders(order_id),
    item_sequence       INTEGER NOT NULL CHECK (item_sequence > 0),
    visible_code        TEXT NOT NULL UNIQUE CHECK (trim(visible_code) <> ''),
    category_id         TEXT NOT NULL REFERENCES categories(category_id),
    brand_id            TEXT NOT NULL REFERENCES brands(brand_id),
    description         TEXT NOT NULL CHECK (trim(description) <> ''),
    due_date            TEXT CHECK (due_date IS NULL OR due_date GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
    is_urgent           INTEGER NOT NULL DEFAULT 0 CHECK (is_urgent IN (0,1)),
    is_priority         INTEGER NOT NULL DEFAULT 0 CHECK (is_priority IN (0,1)),
    workflow_status     TEXT NOT NULL DEFAULT 'DA_LAVORARE'
                        CHECK (workflow_status IN ('DA_LAVORARE','PRESSO_ARTIGIANO','PRONTO_DA_AVVISARE','AVVISATO','CONSEGNATO','ANNULLATO')),
    cancellation_reason TEXT,
    canceled_at         TEXT,
    label_revision      INTEGER NOT NULL DEFAULT 1 CHECK (label_revision >= 1),
    last_printed_label_revision INTEGER CHECK (last_printed_label_revision IS NULL OR last_printed_label_revision >= 1),
    created_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    updated_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    row_version         INTEGER NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    UNIQUE (order_id, item_sequence),
    CHECK ((workflow_status <> 'ANNULLATO') OR (canceled_at IS NOT NULL AND cancellation_reason IS NOT NULL))
) STRICT;

CREATE INDEX ix_items_order ON items(order_id, item_sequence);
CREATE INDEX ix_items_agenda ON items(due_date, workflow_status);
CREATE INDEX ix_items_category_brand ON items(category_id, brand_id);

CREATE TABLE external_assignments (
    external_assignment_id TEXT PRIMARY KEY CHECK (length(external_assignment_id) = 36),
    item_id             TEXT NOT NULL REFERENCES items(item_id),
    artisan_id          TEXT NOT NULL REFERENCES artisans(artisan_id),
    entrusted_at        TEXT NOT NULL,
    expected_return_date TEXT CHECK (expected_return_date IS NULL OR expected_return_date GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
    returned_at         TEXT,
    canceled_at         TEXT,
    notes               TEXT,
    created_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    row_version         INTEGER NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    CHECK (returned_at IS NULL OR returned_at >= entrusted_at)
) STRICT;

CREATE UNIQUE INDEX ux_external_assignment_active
ON external_assignments(item_id) WHERE returned_at IS NULL AND canceled_at IS NULL;
CREATE INDEX ix_external_assignments_artisan ON external_assignments(artisan_id, entrusted_at DESC);

CREATE TABLE item_works (
    item_work_id        TEXT PRIMARY KEY CHECK (length(item_work_id) = 36),
    item_id             TEXT NOT NULL REFERENCES items(item_id),
    source_work_type_id TEXT REFERENCES work_types(work_type_id),
    external_assignment_id TEXT REFERENCES external_assignments(external_assignment_id),
    applied_name        TEXT NOT NULL CHECK (trim(applied_name) <> ''),
    applied_label_text  TEXT NOT NULL CHECK (trim(applied_label_text) <> ''),
    selected_band       TEXT CHECK (selected_band IS NULL OR selected_band IN ('A','B','C')),
    price_status        TEXT NOT NULL DEFAULT 'DEFINITO'
                        CHECK (price_status IN ('DEFINITO','DA_DEFINIRE')),
    price_cents         INTEGER CHECK (price_cents IS NULL OR price_cents >= 0),
    cost_cents          INTEGER CHECK (cost_cents IS NULL OR cost_cents >= 0),
    execution_type      TEXT NOT NULL DEFAULT 'INTERNA'
                        CHECK (execution_type IN ('INTERNA','ESTERNA')),
    approval_status     TEXT NOT NULL DEFAULT 'APPROVATA'
                        CHECK (approval_status IN ('PENDENTE','APPROVATA','RIFIUTATA')),
    incompatibility_confirmed_at TEXT,
    incompatibility_note TEXT,
    sort_order          INTEGER NOT NULL DEFAULT 0,
    canceled_at         TEXT,
    cancellation_reason TEXT,
    created_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    updated_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    row_version         INTEGER NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    CHECK ((price_status = 'DA_DEFINIRE' AND price_cents IS NULL) OR
           (price_status = 'DEFINITO' AND price_cents IS NOT NULL)),
    CHECK ((execution_type = 'INTERNA' AND external_assignment_id IS NULL) OR
           execution_type = 'ESTERNA'),
    CHECK ((canceled_at IS NULL AND cancellation_reason IS NULL) OR
           (canceled_at IS NOT NULL AND cancellation_reason IS NOT NULL))
) STRICT;

CREATE INDEX ix_item_works_item ON item_works(item_id, sort_order);
CREATE INDEX ix_item_works_assignment ON item_works(external_assignment_id);

CREATE TABLE photos (
    photo_id            TEXT PRIMARY KEY CHECK (length(photo_id) = 36),
    item_id             TEXT NOT NULL REFERENCES items(item_id),
    relative_path       TEXT NOT NULL UNIQUE CHECK (trim(relative_path) <> ''),
    original_filename   TEXT,
    extension           TEXT NOT NULL,
    file_size_bytes     INTEGER NOT NULL CHECK (file_size_bytes >= 0),
    sha256              TEXT NOT NULL CHECK (length(sha256) = 64),
    orientation_degrees INTEGER NOT NULL DEFAULT 0 CHECK (orientation_degrees IN (0,90,180,270)),
    sort_order          INTEGER NOT NULL DEFAULT 0,
    acquired_at         TEXT NOT NULL,
    source              TEXT NOT NULL CHECK (source IN ('WEBCAM','IMPORTAZIONE','TELEFONO','MIGRAZIONE')),
    deleted_at          TEXT,
    trash_relative_path TEXT,
    created_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    updated_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    row_version         INTEGER NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    CHECK ((deleted_at IS NULL AND trash_relative_path IS NULL) OR
           (deleted_at IS NOT NULL AND trash_relative_path IS NOT NULL))
) STRICT;

CREATE INDEX ix_photos_item ON photos(item_id, deleted_at, sort_order);

CREATE TABLE payment_events (
    payment_event_id    TEXT PRIMARY KEY CHECK (length(payment_event_id) = 36),
    order_id            TEXT NOT NULL REFERENCES orders(order_id),
    amount_cents        INTEGER NOT NULL CHECK (amount_cents <> 0),
    payment_type        TEXT NOT NULL CHECK (payment_type IN ('CONTANTI','CARTA','BONIFICO','ALTRO','CORREZIONE','RIMBORSO')),
    occurred_at         TEXT NOT NULL,
    corrects_payment_event_id TEXT REFERENCES payment_events(payment_event_id),
    reason              TEXT,
    operator_id         TEXT REFERENCES operators(operator_id),
    transaction_key     TEXT,
    created_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    CHECK ((corrects_payment_event_id IS NULL) OR (reason IS NOT NULL AND trim(reason) <> ''))
) STRICT;

CREATE INDEX ix_payment_events_order ON payment_events(order_id, occurred_at);

CREATE TABLE pickups (
    pickup_id           TEXT PRIMARY KEY CHECK (length(pickup_id) = 36),
    order_id            TEXT NOT NULL REFERENCES orders(order_id),
    occurred_at         TEXT NOT NULL,
    notes               TEXT,
    operator_id         TEXT REFERENCES operators(operator_id),
    transaction_key     TEXT,
    created_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
) STRICT;

CREATE TABLE pickup_items (
    pickup_id           TEXT NOT NULL REFERENCES pickups(pickup_id),
    item_id             TEXT NOT NULL REFERENCES items(item_id),
    PRIMARY KEY (pickup_id, item_id),
    UNIQUE (item_id)
) STRICT;

CREATE TABLE messages (
    message_id          TEXT PRIMARY KEY CHECK (length(message_id) = 36),
    order_id            TEXT NOT NULL REFERENCES orders(order_id),
    message_type        TEXT NOT NULL CHECK (message_type IN ('WHATSAPP','SMS','EMAIL','TELEFONO','ALTRO')),
    purpose             TEXT NOT NULL CHECK (purpose IN ('AVVISO_PRONTO','PREVENTIVO','SOLLECITO','ALTRO')),
    final_text          TEXT NOT NULL,
    recipient           TEXT,
    sent_at             TEXT NOT NULL,
    delivery_status     TEXT NOT NULL DEFAULT 'REGISTRATO'
                        CHECK (delivery_status IN ('REGISTRATO','INVIATO','ERRORE')),
    operator_id         TEXT REFERENCES operators(operator_id),
    created_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
) STRICT;

CREATE TABLE message_items (
    message_id          TEXT NOT NULL REFERENCES messages(message_id),
    item_id             TEXT NOT NULL REFERENCES items(item_id),
    PRIMARY KEY (message_id, item_id)
) STRICT;

CREATE TABLE print_events (
    print_event_id      TEXT PRIMARY KEY CHECK (length(print_event_id) = 36),
    order_id            TEXT REFERENCES orders(order_id),
    item_id             TEXT REFERENCES items(item_id),
    document_type       TEXT NOT NULL CHECK (document_type IN ('COPIA_CLIENTE','COPIA_INTERNA','FOTOGRAFIE','ETICHETTA')),
    template_version    TEXT NOT NULL,
    printer_name        TEXT,
    printed_at          TEXT NOT NULL,
    outcome             TEXT NOT NULL CHECK (outcome IN ('RIUSCITA','ERRORE','ANNULLATA')),
    data_revision       INTEGER,
    label_revision      INTEGER,
    error_message       TEXT,
    operator_id         TEXT REFERENCES operators(operator_id),
    snapshot_json       TEXT CHECK (snapshot_json IS NULL OR json_valid(snapshot_json)),
    created_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    CHECK (order_id IS NOT NULL OR item_id IS NOT NULL),
    CHECK ((document_type = 'ETICHETTA' AND item_id IS NOT NULL) OR document_type <> 'ETICHETTA')
) STRICT;

CREATE INDEX ix_print_events_order ON print_events(order_id, printed_at DESC);
CREATE INDEX ix_print_events_item ON print_events(item_id, printed_at DESC);

CREATE TABLE history_events (
    history_event_id    TEXT PRIMARY KEY CHECK (length(history_event_id) = 36),
    order_id            TEXT REFERENCES orders(order_id),
    item_id             TEXT REFERENCES items(item_id),
    event_type          TEXT NOT NULL,
    occurred_at         TEXT NOT NULL,
    summary             TEXT NOT NULL,
    payload_json        TEXT CHECK (payload_json IS NULL OR json_valid(payload_json)),
    operator_id         TEXT REFERENCES operators(operator_id),
    transaction_key     TEXT,
    created_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    CHECK (order_id IS NOT NULL OR item_id IS NOT NULL)
) STRICT;

CREATE INDEX ix_history_order ON history_events(order_id, occurred_at DESC);
CREATE INDEX ix_history_item ON history_events(item_id, occurred_at DESC);

CREATE TABLE calendar_exceptions (
    calendar_exception_id TEXT PRIMARY KEY CHECK (length(calendar_exception_id) = 36),
    starts_on           TEXT NOT NULL CHECK (starts_on GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
    ends_on             TEXT NOT NULL CHECK (ends_on GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
    exception_type      TEXT NOT NULL CHECK (exception_type IN ('CHIUSURA','CAPACITA_RIDOTTA','APERTURA_STRAORDINARIA','FESTIVITA')),
    capacity_factor     REAL NOT NULL CHECK (capacity_factor IN (0.0,0.5,1.0)),
    description         TEXT NOT NULL,
    recurrence_rule     TEXT,
    created_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    CHECK (ends_on >= starts_on)
) STRICT;

CREATE TABLE backup_runs (
    backup_run_id       TEXT PRIMARY KEY CHECK (length(backup_run_id) = 36),
    run_type            TEXT NOT NULL CHECK (run_type IN ('AUTOMATICO','MANUALE')),
    started_at          TEXT NOT NULL,
    finished_at         TEXT,
    outcome             TEXT NOT NULL CHECK (outcome IN ('IN_CORSO','RIUSCITO','ERRORE')),
    destination_path    TEXT,
    size_bytes          INTEGER CHECK (size_bytes IS NULL OR size_bytes >= 0),
    database_sha256     TEXT CHECK (database_sha256 IS NULL OR length(database_sha256) = 64),
    error_message       TEXT,
    created_at          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    CHECK ((outcome = 'IN_CORSO' AND finished_at IS NULL) OR
           (outcome <> 'IN_CORSO' AND finished_at IS NOT NULL))
) STRICT;

CREATE TABLE legacy_maps (
    legacy_map_id       TEXT PRIMARY KEY CHECK (length(legacy_map_id) = 36),
    source_table        TEXT NOT NULL,
    source_key          TEXT NOT NULL,
    destination_table   TEXT NOT NULL,
    destination_id      TEXT NOT NULL,
    source_snapshot_json TEXT CHECK (source_snapshot_json IS NULL OR json_valid(source_snapshot_json)),
    conversion_notes    TEXT,
    imported_at         TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    UNIQUE (source_table, source_key, destination_table)
) STRICT;

-- Totali calcolati: non vengono duplicati nelle tabelle operative.
CREATE VIEW v_order_financials AS
SELECT
    o.order_id,
    COALESCE(SUM(CASE
        WHEN iw.approval_status = 'APPROVATA'
         AND iw.canceled_at IS NULL
         AND iw.price_status = 'DEFINITO'
        THEN iw.price_cents ELSE 0 END), 0) AS total_cents,
    COALESCE((SELECT SUM(pe.amount_cents)
              FROM payment_events pe
              WHERE pe.order_id = o.order_id), 0) AS paid_cents,
    COALESCE(SUM(CASE
        WHEN iw.approval_status = 'APPROVATA'
         AND iw.canceled_at IS NULL
         AND iw.price_status = 'DEFINITO'
        THEN iw.price_cents ELSE 0 END), 0)
      - COALESCE((SELECT SUM(pe.amount_cents)
                  FROM payment_events pe
                  WHERE pe.order_id = o.order_id), 0) AS balance_cents,
    SUM(CASE
        WHEN iw.approval_status <> 'RIFIUTATA'
         AND iw.canceled_at IS NULL
         AND iw.price_status = 'DA_DEFINIRE'
        THEN 1 ELSE 0 END) AS undefined_price_count
FROM orders o
LEFT JOIN items i ON i.order_id = o.order_id
LEFT JOIN item_works iw ON iw.item_id = i.item_id
GROUP BY o.order_id;

CREATE VIEW v_items_to_reprint_label AS
SELECT i.*
FROM items i
WHERE i.last_printed_label_revision IS NULL
   OR i.last_printed_label_revision <> i.label_revision;

CREATE VIEW v_active_external_assignments AS
SELECT ea.*, a.display_name AS artisan_name, i.visible_code AS item_code
FROM external_assignments ea
JOIN artisans a ON a.artisan_id = ea.artisan_id
JOIN items i ON i.item_id = ea.item_id
WHERE ea.returned_at IS NULL AND ea.canceled_at IS NULL;

-- Una variazione della consegna rende l'etichetta da ristampare.
CREATE TRIGGER tr_items_due_date_label_revision
AFTER UPDATE OF due_date ON items
WHEN OLD.due_date IS NOT NEW.due_date
BEGIN
    UPDATE items
       SET label_revision = label_revision + 1,
           updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'),
           row_version = row_version + 1
     WHERE item_id = NEW.item_id;
END;

-- Inserimenti, modifiche o annullamenti delle lavorazioni aggiornano l'etichetta.
CREATE TRIGGER tr_item_works_insert_label_revision
AFTER INSERT ON item_works
BEGIN
    UPDATE items
       SET label_revision = label_revision + 1,
           updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'),
           row_version = row_version + 1
     WHERE item_id = NEW.item_id;
END;

CREATE TRIGGER tr_item_works_update_label_revision
AFTER UPDATE OF applied_label_text, execution_type, approval_status, canceled_at ON item_works
WHEN OLD.applied_label_text IS NOT NEW.applied_label_text
  OR OLD.execution_type IS NOT NEW.execution_type
  OR OLD.approval_status IS NOT NEW.approval_status
  OR OLD.canceled_at IS NOT NEW.canceled_at
BEGIN
    UPDATE items
       SET label_revision = label_revision + 1,
           updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'),
           row_version = row_version + 1
     WHERE item_id = NEW.item_id;
END;

CREATE TRIGGER tr_item_works_delete_forbidden
BEFORE DELETE ON item_works
BEGIN
    SELECT RAISE(ABORT, 'Le lavorazioni devono essere annullate, non eliminate');
END;

-- La lavorazione esterna deve appartenere allo stesso articolo dell'affidamento.
CREATE TRIGGER tr_item_works_external_assignment_insert
BEFORE INSERT ON item_works
WHEN NEW.external_assignment_id IS NOT NULL
BEGIN
    SELECT CASE WHEN NOT EXISTS (
        SELECT 1 FROM external_assignments ea
        WHERE ea.external_assignment_id = NEW.external_assignment_id
          AND ea.item_id = NEW.item_id
    ) THEN RAISE(ABORT, 'Affidamento esterno riferito a un altro articolo') END;
END;

CREATE TRIGGER tr_item_works_external_assignment_update
BEFORE UPDATE OF item_id, external_assignment_id ON item_works
WHEN NEW.external_assignment_id IS NOT NULL
BEGIN
    SELECT CASE WHEN NOT EXISTS (
        SELECT 1 FROM external_assignments ea
        WHERE ea.external_assignment_id = NEW.external_assignment_id
          AND ea.item_id = NEW.item_id
    ) THEN RAISE(ABORT, 'Affidamento esterno riferito a un altro articolo') END;
END;

-- Non è possibile segnare Pronto se una lavorazione esterna attiva non ha il costo.
CREATE TRIGGER tr_items_ready_requires_external_cost
BEFORE UPDATE OF workflow_status ON items
WHEN NEW.workflow_status IN ('PRONTO_DA_AVVISARE','AVVISATO','CONSEGNATO')
BEGIN
    SELECT CASE WHEN EXISTS (
        SELECT 1 FROM item_works iw
        WHERE iw.item_id = NEW.item_id
          AND iw.execution_type = 'ESTERNA'
          AND iw.approval_status = 'APPROVATA'
          AND iw.canceled_at IS NULL
          AND iw.cost_cents IS NULL
    ) THEN RAISE(ABORT, 'Costo artigiano mancante: articolo non impostabile come Pronto') END;
END;

-- Gli eventi economici e di tracciamento non si riscrivono né si cancellano.
CREATE TRIGGER tr_payment_events_no_update BEFORE UPDATE ON payment_events
BEGIN SELECT RAISE(ABORT, 'I pagamenti si correggono con un nuovo evento'); END;
CREATE TRIGGER tr_payment_events_no_delete BEFORE DELETE ON payment_events
BEGIN SELECT RAISE(ABORT, 'I pagamenti non possono essere eliminati'); END;
CREATE TRIGGER tr_pickups_no_update BEFORE UPDATE ON pickups
BEGIN SELECT RAISE(ABORT, 'I ritiri non possono essere modificati'); END;
CREATE TRIGGER tr_pickups_no_delete BEFORE DELETE ON pickups
BEGIN SELECT RAISE(ABORT, 'I ritiri non possono essere eliminati'); END;
CREATE TRIGGER tr_pickup_items_no_update BEFORE UPDATE ON pickup_items
BEGIN SELECT RAISE(ABORT, 'Il dettaglio ritiro non può essere modificato'); END;
CREATE TRIGGER tr_pickup_items_no_delete BEFORE DELETE ON pickup_items
BEGIN SELECT RAISE(ABORT, 'Il dettaglio ritiro non può essere eliminato'); END;
CREATE TRIGGER tr_messages_no_update BEFORE UPDATE ON messages
BEGIN SELECT RAISE(ABORT, 'I messaggi inviati non possono essere modificati'); END;
CREATE TRIGGER tr_messages_no_delete BEFORE DELETE ON messages
BEGIN SELECT RAISE(ABORT, 'I messaggi inviati non possono essere eliminati'); END;
CREATE TRIGGER tr_message_items_no_update BEFORE UPDATE ON message_items
BEGIN SELECT RAISE(ABORT, 'Il dettaglio messaggio non può essere modificato'); END;
CREATE TRIGGER tr_message_items_no_delete BEFORE DELETE ON message_items
BEGIN SELECT RAISE(ABORT, 'Il dettaglio messaggio non può essere eliminato'); END;
CREATE TRIGGER tr_print_events_no_update BEFORE UPDATE ON print_events
BEGIN SELECT RAISE(ABORT, 'Gli eventi di stampa non possono essere modificati'); END;
CREATE TRIGGER tr_print_events_no_delete BEFORE DELETE ON print_events
BEGIN SELECT RAISE(ABORT, 'Gli eventi di stampa non possono essere eliminati'); END;
CREATE TRIGGER tr_history_events_no_update BEFORE UPDATE ON history_events
BEGIN SELECT RAISE(ABORT, 'La cronologia non può essere modificata'); END;
CREATE TRIGGER tr_history_events_no_delete BEFORE DELETE ON history_events
BEGIN SELECT RAISE(ABORT, 'La cronologia non può essere eliminata'); END;

INSERT INTO schema_migrations(version, name) VALUES (1, 'schema_solo_lab_iniziale');
INSERT INTO schema_migrations(version, name) VALUES (2, 'tempo_lavorazioni_e_capacita_giornaliera');

COMMIT;

