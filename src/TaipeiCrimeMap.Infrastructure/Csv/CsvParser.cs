using System.Text;
using Microsoft.Extensions.Logging;
using TaipeiCrimeMap.Domain.Aggregates;
using TaipeiCrimeMap.Domain.Results;
using TaipeiCrimeMap.Domain.Services;
using TaipeiCrimeMap.Domain.ValueObjects;

namespace TaipeiCrimeMap.Infrastructure.Csv;

public class CsvParser : ICsvParser
{
    private readonly ILogger<CsvParser> _logger;

    static CsvParser()
    {   // 以支援 cp950 & Big5 編碼
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public CsvParser(ILogger<CsvParser> logger)
    {
        _logger = logger;
    }

    public CsvParseResult Parse(string filePath, CaseType expectedCaseType)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var encoding = DetectEncoding(filePath);
        var results = new List<TheftCase>();
        var rowNumber = 0;
        var skppedCount = 0;

        foreach (var line in File.ReadLines(filePath, encoding))
        {
            rowNumber++;

            if (rowNumber == 1)
            {
                continue; // Skip header line
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                skppedCount++;
                continue; // Skip empty lines
            }

            var colunms = line.Split(',');
            if (colunms.Length < 5)
            {
                skppedCount++;
                _logger.LogWarning("第 {Row} 列欄位數不足，跳過: {Line}", rowNumber, line);
                continue;
            }

            var caseNumber = colunms[0].Trim();
            var rawCaseType = colunms[1].Trim();
            var rawDate = colunms[2].Trim();
            var rawTimeSlot = colunms[3].Trim();
            var rawLocation = colunms[4].Trim();

            var parsedCaseType = CaseTypeExtensions.FromChineseName(rawCaseType);
            if (parsedCaseType is null)
            {
                skppedCount++;
                _logger.LogWarning("第 {Row} 列案類無法識別: {CaseType}，跳過", rowNumber, rawCaseType);
                continue;
            }

            if (parsedCaseType.Value != expectedCaseType)
            {
                skppedCount++;
                _logger.LogWarning("第 {Row} 列案類不符，預期 {Expected}，實際 {Actual}，跳過",
                    rowNumber, expectedCaseType, parsedCaseType);
                continue;
            }

            if (string.IsNullOrWhiteSpace(rawLocation))
            {
                skppedCount++;
                _logger.LogWarning("第 {Row} 列發生地點為空，跳過", rowNumber);
                continue;
            }

            var occurredDate = TaiwanDate.Parse(rawDate);
            var timeSlot = string.IsNullOrWhiteSpace(rawTimeSlot) ? null : TimeSlot.Parse(rawTimeSlot);
            var district = District.ParseFrom(rawLocation);

            var theftCase = TheftCase.Create(
                caseNumber: caseNumber,
                caseType: parsedCaseType.Value,
                district: district,
                occurredDate: occurredDate,
                timeSlot: timeSlot, 
                rawLocation: rawLocation);
            
            results.Add(theftCase);
        }
        
        _logger.LogInformation("CSV 解析完成：{FilePath}，共 {Count} 筆", filePath, results.Count);

        return new CsvParseResult(results, skppedCount);
    }

    private static Encoding DetectEncoding(string filePath)
    {
        var bom = new byte[4];
        using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        {
            fs.ReadAtLeast(bom, minimumBytes: 4, throwOnEndOfStream: false);
        }

        if (bom[0] == 0xFF && bom[1] == 0xFE)
        {
            return Encoding.Unicode; // UTF-16 LE
        }

        if (bom[0] == 0xFE && bom[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode; // UTF-16 BE
        }

        if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
        {
            return Encoding.UTF8; // UTF-8 with BOM
        }

        return Encoding.GetEncoding(950);
    }
}