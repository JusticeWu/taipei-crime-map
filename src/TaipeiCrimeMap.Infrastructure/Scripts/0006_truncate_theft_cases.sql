-- Reset data to eliminate duplicates from repeated CI imports.
-- The CI import step will reload clean data after this truncation.
TRUNCATE TABLE theft_cases;
