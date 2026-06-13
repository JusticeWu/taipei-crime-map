-- occurred_year + 1911 >= @YearFrom 這種寫法會讓 occurred_year 欄位被算式包住，
-- 造成 ix_theft_cases_occurred_year 索引無法被 Index Seek 使用（non-sargable）。
-- 改為把運算移到參數側：occurred_year >= @YearFrom - 1911，邏輯等價但可走索引。
CREATE OR ALTER PROCEDURE sp_get_theft_cases_by_filter
    @CaseType       INT             = NULL,
    @District       NVARCHAR(10)    = NULL,
    @YearFrom       INT             = NULL,
    @YearTo         INT             = NULL,
    @TimeSlotStart  INT             = NULL,
    @TimeSlotEnd    INT             = NULL,
    @Page           INT             = 1,
    @PageSize       INT             = 2147483647
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @offset INT = (@Page - 1) * @PageSize;

    DECLARE @sql NVARCHAR(MAX) = N'
        SELECT *, COUNT(*) OVER() AS total_count
        FROM theft_cases WITH (NOLOCK)
        WHERE 1=1';

    DECLARE @params NVARCHAR(MAX) = N'
        @CaseType INT, @District NVARCHAR(10),
        @YearFrom INT, @YearTo INT,
        @TimeSlotStart INT, @TimeSlotEnd INT,
        @Offset INT, @PageSize INT';

    IF @CaseType      IS NOT NULL  SET @sql = @sql + N' AND case_type       = @CaseType';
    IF @District      IS NOT NULL  SET @sql = @sql + N' AND district        = @District';
    IF @YearFrom      IS NOT NULL  SET @sql = @sql + N' AND occurred_year >= @YearFrom - 1911';
    IF @YearTo        IS NOT NULL  SET @sql = @sql + N' AND occurred_year <= @YearTo   - 1911';
    IF @TimeSlotStart IS NOT NULL  SET @sql = @sql + N' AND time_slot_start = @TimeSlotStart';
    IF @TimeSlotEnd   IS NOT NULL  SET @sql = @sql + N' AND time_slot_end   = @TimeSlotEnd';

    SET @sql = @sql + N' ORDER BY id OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY';

    EXEC sp_executesql @sql, @params,
        @CaseType      = @CaseType,
        @District      = @District,
        @YearFrom      = @YearFrom,
        @YearTo        = @YearTo,
        @TimeSlotStart = @TimeSlotStart,
        @TimeSlotEnd   = @TimeSlotEnd,
        @Offset        = @offset,
        @PageSize      = @PageSize;
END;
