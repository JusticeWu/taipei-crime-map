CREATE OR ALTER PROCEDURE sp_get_theft_cases_by_filter
    @CaseType       INT             = NULL,
    @District       NVARCHAR(10)    = NULL,
    @YearFrom       INT             = NULL,
    @YearTo         INT             = NULL,
    @TimeSlotStart  INT             = NULL,
    @TimeSlotEnd    INT             = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @sql    NVARCHAR(MAX) = N'SELECT * FROM theft_cases WHERE 1=1';
    DECLARE @params NVARCHAR(MAX) = N'@CaseType INT, @District NVARCHAR(10), @YearFrom INT, @YearTo INT, @TimeSlotStart INT, @TimeSlotEnd INT';

    IF @CaseType      IS NOT NULL  SET @sql = @sql + N' AND case_type       = @CaseType';
    IF @District      IS NOT NULL  SET @sql = @sql + N' AND district        = @District';
    IF @YearFrom      IS NOT NULL  SET @sql = @sql + N' AND occurred_year   >= @YearFrom';
    IF @YearTo        IS NOT NULL  SET @sql = @sql + N' AND occurred_year   <= @YearTo';
    IF @TimeSlotStart IS NOT NULL  SET @sql = @sql + N' AND time_slot_start = @TimeSlotStart';
    IF @TimeSlotEnd   IS NOT NULL  SET @sql = @sql + N' AND time_slot_end   = @TimeSlotEnd';

    EXEC sp_executesql @sql, @params,
        @CaseType      = @CaseType,
        @District      = @District,
        @YearFrom      = @YearFrom,
        @YearTo        = @YearTo,
        @TimeSlotStart = @TimeSlotStart,
        @TimeSlotEnd   = @TimeSlotEnd;
END;
