CREATE TABLE IF NOT EXISTS theft_cases (
    id                  UUID            NOT NULL DEFAULT gen_random_uuid(),
    case_number         VARCHAR(50)     NOT NULL,
    case_type           INTEGER,
    district            VARCHAR(10),
    occurred_date_raw   VARCHAR(7)      NOT NULL,
    occurred_date       DATE,
    occurred_year       INTEGER,
    time_slot_raw       VARCHAR(20),
    time_slot_start     INTEGER,
    time_slot_end       INTEGER,
    raw_location        VARCHAR(200)    NOT NULL,
    latitude            DOUBLE PRECISION,
    longitude           DOUBLE PRECISION,
    imported_at         TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),

    CONSTRAINT pk_theft_cases PRIMARY KEY (id),
    CONSTRAINT uq_theft_cases_case_number UNIQUE (case_number)
);

CREATE INDEX IF NOT EXISTS ix_theft_cases_case_type     ON theft_cases (case_type);
CREATE INDEX IF NOT EXISTS ix_theft_cases_district      ON theft_cases (district);
CREATE INDEX IF NOT EXISTS ix_theft_cases_occurred_year ON theft_cases (occurred_year);