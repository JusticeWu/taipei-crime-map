IF EXISTS (SELECT * FROM sys.key_constraints WHERE name = 'uq_theft_cases_case_number' AND parent_object_id = OBJECT_ID('theft_cases'))
    ALTER TABLE theft_cases DROP CONSTRAINT uq_theft_cases_case_number;
