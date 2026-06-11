-- occurred_year 欄位儲存的是民國年（如 113），但前端傳入的 YearFrom/YearTo 是西元年（如 2024）。
-- 統一在 SP 內把 occurred_year 轉換為西元年（+1911）後再比對。
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
    IF @YearFrom      IS NOT NULL  SET @sql = @sql + N' AND occurred_year + 1911 >= @YearFrom';
    IF @YearTo        IS NOT NULL  SET @sql = @sql + N' AND occurred_year + 1911 <= @YearTo';
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
