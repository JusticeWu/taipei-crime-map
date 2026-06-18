-- 先移除依賴 case_number 的唯一索引
DROP INDEX IF EXISTS UX_TheftCases_CaseType_CaseNumber ON theft_cases;

-- 將 case_number 欄位從 NVARCHAR 改為 INT
ALTER TABLE theft_cases
ALTER COLUMN case_number INT NOT NULL;

-- 重建唯一索引
CREATE UNIQUE INDEX UX_TheftCases_CaseType_CaseNumber
    ON theft_cases (case_type, case_number);

GO

-- 重建 SP：case_number 現在是 INT，排序直接用欄位名
CREATE OR ALTER PROCEDURE sp_get_theft_cases_by_filter
    @CaseType       INT             = NULL,
    @District       NVARCHAR(10)    = NULL,
    @YearFrom       INT             = NULL,
    @YearTo         INT             = NULL,
    @TimeSlotStart  INT             = NULL,
    @TimeSlotEnd    INT             = NULL,
    @Page           INT             = 1,
    @PageSize       INT             = 2147483647,
    @SortBy         NVARCHAR(20)    = NULL,
    @SortOrder      NVARCHAR(4)     = NULL
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

    DECLARE @sortCol NVARCHAR(50) = CASE @SortBy
        WHEN 'caseNumber'     THEN 'case_number'
        WHEN 'occurrenceDate' THEN 'occurred_date'
        WHEN 'district'       THEN 'district'
        ELSE 'case_number'
    END;

    DECLARE @sortDir NVARCHAR(4) = CASE WHEN @SortOrder = 'desc' THEN 'DESC' ELSE 'ASC' END;

    SET @sql = @sql + N' ORDER BY ' + @sortCol + N' ' + @sortDir
             + N' OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY';

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
