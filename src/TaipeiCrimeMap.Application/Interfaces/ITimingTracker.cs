namespace TaipeiCrimeMap.Application.Interfaces;

/// <summary>
/// 記錄一次請求中各階段的執行時間。
/// 當 Timing:Enabled = false 時，注入 NullTimingTracker（不做任何事）。
/// </summary>
public interface ITimingTracker
{
    /// <summary>開始計時一個階段，回傳 IDisposable，Dispose 時自動停止並記錄。</summary>
    IDisposable Track(string stageName);

    /// <summary>將本次所有階段的時間寫入 log。</summary>
    void LogSummary();
}
