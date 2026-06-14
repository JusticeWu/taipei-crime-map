using System.Globalization;
using System.Text.RegularExpressions;

namespace TaipeiCrimeMap.Infrastructure.Geocoding;

// 在呼叫 Google Maps Geocoding API 之前，對地址做正規化處理，
// 提高地址的可定位性。不修改資料庫中的 raw_location，僅用於查詢。
public static partial class AddressNormalizer
{
    public static string Normalize(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return address;

        var result = address;

        // 規則一：「路路」取代為「路」
        result = result.Replace("路路", "路");

        // 規則二：「一帶」取代為空字串
        result = result.Replace("一帶", string.Empty);

        // 規則三：移除結尾的括弧及括弧內的內容，例如「景文街(景中街口)」→「景文街」
        result = TrailingParenthesesRegex().Replace(result, string.Empty);

        // 規則四：門牌號碼範圍取平均值，例如「31~60號」→「45號」
        result = NumberRangeRegex().Replace(result, match =>
        {
            var from = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var to = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            var average = (from + to) / 2;
            return average.ToString(CultureInfo.InvariantCulture);
        });

        return result;
    }

    [GeneratedRegex(@"\([^)]*\)$")]
    private static partial Regex TrailingParenthesesRegex();

    [GeneratedRegex(@"(\d+)\s*[-~～]\s*(\d+)")]
    private static partial Regex NumberRangeRegex();
}
