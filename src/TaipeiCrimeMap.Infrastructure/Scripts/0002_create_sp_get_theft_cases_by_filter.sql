CREATE OR REPLACE FUNCTION sp_get_theft_cases_by_filter(
    p_case_type         INTEGER  DEFAULT NULL,
    p_district          VARCHAR  DEFAULT NULL,
    p_year_from         INTEGER  DEFAULT NULL,
    p_year_to           INTEGER  DEFAULT NULL,
    p_time_slot_start   INTEGER  DEFAULT NULL,
    p_time_slot_end     INTEGER  DEFAULT NULL
)
RETURNS SETOF theft_cases
LANGUAGE plpgsql
AS $$
DECLARE
    v_sql TEXT;
BEGIN
    v_sql :=
        'SELECT * FROM theft_cases WHERE 1=1'
        || CASE WHEN p_case_type       IS NOT NULL THEN ' AND case_type       = $1' ELSE '' END
        || CASE WHEN p_district        IS NOT NULL THEN ' AND district        = $2' ELSE '' END
        || CASE WHEN p_year_from       IS NOT NULL THEN ' AND occurred_year   >= $3' ELSE '' END
        || CASE WHEN p_year_to         IS NOT NULL THEN ' AND occurred_year   <= $4' ELSE '' END
        || CASE WHEN p_time_slot_start IS NOT NULL THEN ' AND time_slot_start = $5' ELSE '' END
        || CASE WHEN p_time_slot_end   IS NOT NULL THEN ' AND time_slot_end   = $6' ELSE '' END;

    RETURN QUERY EXECUTE v_sql
        USING
            p_case_type,
            p_district,
            p_year_from,
            p_year_to,
            p_time_slot_start,
            p_time_slot_end;
END;
$$;