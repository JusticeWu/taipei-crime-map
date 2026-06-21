using TaipeiCrimeMap.Domain.Aggregates;

namespace TaipeiCrimeMap.Domain.Services;

public static class AnalysisGrouping
{
    private static readonly Dictionary<string, string> DistrictToGroup = new()
    {
        ["中正區"] = "G1", ["大同區"] = "G1", ["萬華區"] = "G1",
        ["松山區"] = "G2", ["信義區"] = "G2", ["大安區"] = "G2", ["中山區"] = "G2",
        ["士林區"] = "G3", ["北投區"] = "G3",
        ["內湖區"] = "G4", ["南港區"] = "G4",
        ["文山區"] = "G5",
    };

    private static readonly Dictionary<string, IReadOnlyList<string>> GroupToDistricts = new()
    {
        ["G1"] = new[] { "中正區", "大同區", "萬華區" },
        ["G2"] = new[] { "松山區", "信義區", "大安區", "中山區" },
        ["G3"] = new[] { "士林區", "北投區" },
        ["G4"] = new[] { "內湖區", "南港區" },
        ["G5"] = new[] { "文山區" },
    };

    private static readonly Dictionary<CaseType, string> CaseTypeToGroup = new()
    {
        [CaseType.Residential] = "C1",
        [CaseType.Car] = "C2", [CaseType.Motorcycle] = "C2", [CaseType.Bicycle] = "C2",
        [CaseType.Snatching] = "C3", [CaseType.Robbery] = "C3",
    };

    private static readonly Dictionary<string, IReadOnlyList<int>> GroupToCaseTypes = new()
    {
        ["C1"] = new[] { (int)CaseType.Residential },
        ["C2"] = new[] { (int)CaseType.Car, (int)CaseType.Motorcycle, (int)CaseType.Bicycle },
        ["C3"] = new[] { (int)CaseType.Snatching, (int)CaseType.Robbery },
    };

    private static readonly Dictionary<string, string> GroupToCaseTypeLabel = new()
    {
        ["C1"] = "住宅竊盜",
        ["C2"] = "汽車竊盜、機車竊盜、自行車竊盜",
        ["C3"] = "搶奪、強盜",
    };

    private static readonly Dictionary<string, string> GroupToTimeSlotLabel = new()
    {
        ["T1"] = "00~04 時",
        ["T2"] = "04~08 時",
        ["T3"] = "08~12 時",
        ["T4"] = "12~16 時",
        ["T5"] = "16~20 時",
        ["T6"] = "20~24 時",
    };

    public static string GetDistrictGroup(string districtName) =>
        DistrictToGroup.TryGetValue(districtName, out var g) ? g : "G5";

    public static string GetCaseTypeGroup(CaseType caseType) =>
        CaseTypeToGroup.TryGetValue(caseType, out var g) ? g : "C1";

    public static string GetTimeSlotGroup(int startHour) => startHour switch
    {
        >= 0 and < 4 => "T1",
        >= 4 and < 8 => "T2",
        >= 8 and < 12 => "T3",
        >= 12 and < 16 => "T4",
        >= 16 and < 20 => "T5",
        _ => "T6",
    };

    public static (int MinHour, int MaxHour) GetTimeSlotRange(string group) => group switch
    {
        "T1" => (0, 4),
        "T2" => (4, 8),
        "T3" => (8, 12),
        "T4" => (12, 16),
        "T5" => (16, 20),
        "T6" => (20, 24),
        _ => (0, 24),
    };

    public static IReadOnlyList<string> GetDistrictsInGroup(string group) =>
        GroupToDistricts.TryGetValue(group, out var d) ? d : Array.Empty<string>();

    public static IReadOnlyList<int> GetCaseTypesInGroup(string group) =>
        GroupToCaseTypes.TryGetValue(group, out var c) ? c : Array.Empty<int>();

    public static string GetDistrictGroupLabel(string group) =>
        string.Join("、", GetDistrictsInGroup(group));

    public static string GetCaseTypeGroupLabel(string group) =>
        GroupToCaseTypeLabel.TryGetValue(group, out var l) ? l : group;

    public static string GetTimeSlotGroupLabel(string group) =>
        GroupToTimeSlotLabel.TryGetValue(group, out var l) ? l : group;
}
