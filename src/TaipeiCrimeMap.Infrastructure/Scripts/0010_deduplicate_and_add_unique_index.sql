-- 清除重複資料（CaseType + CaseNumber 相同的筆數 > 1）
-- 保留規則：優先保留有座標的；若都有或都無座標，保留 CreatedAt 較早的
;WITH ranked AS (
    SELECT id,
           ROW_NUMBER() OVER (
               PARTITION BY case_type, case_number
               ORDER BY
                   CASE WHEN latitude IS NOT NULL THEN 0 ELSE 1 END,
                   created_at ASC
           ) AS rn
    FROM theft_cases
)
DELETE FROM ranked WHERE rn > 1;

-- 建立唯一索引，防止未來再出現重複
CREATE UNIQUE INDEX UX_TheftCases_CaseType_CaseNumber
    ON theft_cases (case_type, case_number);
