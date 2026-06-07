IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'theft_cases')
BEGIN
    CREATE TABLE theft_cases (
        id                  UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWID(),
        case_number         NVARCHAR(50)        NOT NULL,
        case_type           INT,
        district            NVARCHAR(10),
        occurred_date_raw   NVARCHAR(7)         NOT NULL,
        occurred_date       DATE,
        occurred_year       INT,
        time_slot_raw       NVARCHAR(20),
        time_slot_start     INT,
        time_slot_end       INT,
        raw_location        NVARCHAR(200)       NOT NULL,
        latitude            FLOAT,
        longitude           FLOAT,
        imported_at         DATETIMEOFFSET      NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        created_at          DATETIMEOFFSET      NOT NULL DEFAULT SYSDATETIMEOFFSET(),

        CONSTRAINT pk_theft_cases PRIMARY KEY (id),
        CONSTRAINT uq_theft_cases_case_number UNIQUE (case_number)
    );
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ix_theft_cases_case_type' AND object_id = OBJECT_ID('theft_cases'))
    CREATE INDEX ix_theft_cases_case_type     ON theft_cases (case_type);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ix_theft_cases_district' AND object_id = OBJECT_ID('theft_cases'))
    CREATE INDEX ix_theft_cases_district      ON theft_cases (district);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'ix_theft_cases_occurred_year' AND object_id = OBJECT_ID('theft_cases'))
    CREATE INDEX ix_theft_cases_occurred_year ON theft_cases (occurred_year);
