IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_theft_cases_case_number' AND object_id = OBJECT_ID('theft_cases'))
    DROP INDEX IX_theft_cases_case_number ON theft_cases;
