using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using CodeBurnMenubar.Models;
using CodeBurnMenubar.Data;

namespace CodeBurnMenubar.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public MainViewModel()
    {
        _ = RefreshAsync();
    }

    [ObservableProperty] private string selectedProvider = "all";
    [ObservableProperty] private string selectedPeriod = "today";
    [ObservableProperty] private string selectedTab = "Trend";
    [ObservableProperty] private bool isLoading = true;
    [ObservableProperty] private string? lastError;
    [ObservableProperty] private MenubarPayload? payload;
    [ObservableProperty] private MenubarPayload? todayPayload;

    public IRelayCommand? CloseCommand { get; set; }

    // --- Derived state ---

    public bool HasError => !string.IsNullOrEmpty(LastError);
    public bool HasData => Payload != null && !HasError;

    public bool IsAllProvider => SelectedProvider == "all";
    public bool IsClaudeProvider => SelectedProvider == "claude";
    public bool IsCodexProvider => SelectedProvider == "codex";
    public bool IsCursorProvider => SelectedProvider == "cursor";
    public bool IsPilotProvider => SelectedProvider == "pilot";

    public bool IsTodayPeriod => SelectedPeriod == "today";
    public bool Is7DPeriod => SelectedPeriod == "7d";
    public bool Is30DPeriod => SelectedPeriod == "30d";
    public bool IsMonthPeriod => SelectedPeriod == "month";
    public bool IsAllPeriod => SelectedPeriod == "all";

    public bool IsTrendTab => SelectedTab == "Trend";
    public bool IsForecastTab => SelectedTab == "Forecast";
    public bool IsPulseTab => SelectedTab == "Pulse";
    public bool IsStatsTab => SelectedTab == "Stats";

    public string DateDisplay
    {
        get
        {
            var now = DateTime.Now;
            return $"Today ({now:yyyy-MM-dd}) · {now:ddd MMM d}";
        }
    }

    public string CostDisplay => Payload != null ? $"${Payload.Current.Cost:N2}" : "—";
    public string CallsDisplay => Payload != null ? $"{Payload.Current.Calls:N0} calls" : "—";
    public string SessionsDisplay => Payload != null ? $"{Payload.Current.Sessions:N0} sessions" : "";

    // History filtered to the selected period — drives all trend-section values
    private IReadOnlyList<DailyHistoryEntry> PeriodHistory
    {
        get
        {
            var all = Payload?.History.Daily;
            if (all == null || all.Count == 0) return [];
            var cutoff = SelectedPeriod switch
            {
                "today" => DateTime.Today,
                "7d"    => DateTime.Today.AddDays(-7),
                "30d"   => DateTime.Today.AddDays(-30),
                "month" => new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
                _       => DateTime.MinValue,
            };
            if (cutoff == DateTime.MinValue) return all;
            return all.Where(d => DateTime.TryParse(d.Date, out var dt) && dt.Date >= cutoff).ToList();
        }
    }

    public int DayCount => PeriodHistory.Count;

    public string TotalTokensLabel
    {
        get
        {
            var total = PeriodHistory.Sum(d => (long)(d.InputTokens + d.OutputTokens));
            return FormatTokens(total);
        }
    }

    public string AvgDailyTokensLabel
    {
        get
        {
            var ph = PeriodHistory;
            if (ph.Count == 0) return "—";
            var total = ph.Sum(d => (long)(d.InputTokens + d.OutputTokens));
            return FormatTokens(total / ph.Count);
        }
    }

    public string PeakTokensLabel
    {
        get
        {
            var ph = PeriodHistory;
            if (ph.Count == 0) return "—";
            return FormatTokens(ph.Max(d => d.InputTokens + d.OutputTokens));
        }
    }

    public string PeakDateLabel
    {
        get
        {
            var ph = PeriodHistory;
            if (ph.Count == 0) return "";
            var entry = ph.OrderByDescending(d => d.InputTokens + d.OutputTokens).First();
            return DateTime.TryParse(entry.Date, out var dt) ? dt.ToString("MM/dd") : entry.Date;
        }
    }

    public string YesterdayTokensLabel
    {
        get
        {
            var all = Payload?.History.Daily;
            if (all == null || all.Count < 2) return "—";
            var yesterday = all[all.Count - 2];
            return FormatTokens(yesterday.InputTokens + yesterday.OutputTokens);
        }
    }

    public IReadOnlyList<ChartBarItem> ChartBars
    {
        get
        {
            var ph = PeriodHistory;
            if (ph.Count == 0) return [];
            var max = ph.Max(d => d.InputTokens + d.OutputTokens);
            if (max == 0) return [];
            const double maxH = 80;
            var peakIdx = ph
                .Select((d, i) => (d, i))
                .OrderByDescending(x => x.d.InputTokens + x.d.OutputTokens)
                .First().i;
            return ph.Select((d, i) => new ChartBarItem
            {
                Tokens = d.InputTokens + d.OutputTokens,
                BarHeight = Math.Max(3, (d.InputTokens + d.OutputTokens) / (double)max * maxH),
                IsHighlighted = i == peakIdx
            }).ToList();
        }
    }

    public IReadOnlyList<ActivityBarItem> ActivityBars
    {
        get
        {
            var acts = Payload?.Current.Activities;
            if (acts == null || acts.Count == 0) return [];
            var maxCost = acts.Max(a => a.Cost);
            if (maxCost == 0) return [];
            return acts.Select(a => new ActivityBarItem
            {
                Label = a.Label,
                Cost = a.Cost,
                Turns = a.Turns,
                OneShotPct = a.OneShotPct,
                BarWidth = Math.Max(4, a.Cost / maxCost * 120)
            }).ToList();
        }
    }

    public bool HasActivities => (Payload?.Current.Activities?.Count ?? 0) > 0;

    // --- Forecast tab ---

    private static int DaysInCurrentMonth => DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month);
    private static int DayOfMonth => DateTime.Now.Day;

    public string ProjectedMonthlyCostLabel
    {
        get
        {
            var days = Payload?.History.Daily.Count ?? 0;
            if (days == 0) return "—";
            var dailyAvg = Payload!.Current.Cost / days;
            return $"${dailyAvg * DaysInCurrentMonth:N2}";
        }
    }

    public string DailyBurnRateLabel
    {
        get
        {
            var days = Payload?.History.Daily.Count ?? 0;
            if (days == 0) return "—";
            return $"${Payload!.Current.Cost / days:N2}/day";
        }
    }

    public string MonthProgressLabel => $"{DayOfMonth} of {DaysInCurrentMonth} days";
    public double MonthProgressWidth => DayOfMonth / (double)DaysInCurrentMonth * 300;

    // --- Pulse tab ---

    public string CostPerCallLabel
    {
        get
        {
            var calls = Payload?.Current.Calls ?? 0;
            if (calls == 0) return "—";
            return $"${Payload!.Current.Cost / calls:N3}";
        }
    }

    public string AvgCallsPerDayLabel
    {
        get
        {
            var days = Payload?.History.Daily.Count ?? 0;
            if (days == 0) return "—";
            var total = Payload!.History.Daily.Sum(d => d.Calls);
            return $"{total / days:N0}";
        }
    }

    public string BusiestDayLabel
    {
        get
        {
            var daily = Payload?.History.Daily;
            if (daily == null || daily.Count == 0) return "—";
            var busiest = daily.OrderByDescending(d => d.Calls).First();
            if (!DateTime.TryParse(busiest.Date, out var dt))
                return $"{busiest.Calls} calls";
            return $"{dt:MMM d} ({busiest.Calls} calls)";
        }
    }

    public string TokenRatioLabel
    {
        get
        {
            var daily = Payload?.History.Daily;
            if (daily == null || daily.Count == 0) return "—";
            var totalIn = daily.Sum(d => (long)d.InputTokens);
            var totalOut = daily.Sum(d => (long)d.OutputTokens);
            var total = totalIn + totalOut;
            if (total == 0) return "—";
            return $"{totalIn * 100 / total}% in · {totalOut * 100 / total}% out";
        }
    }

    // --- Stats tab ---

    public string TotalInputTokensLabel
    {
        get => FormatTokens(Payload?.History.Daily.Sum(d => (long)d.InputTokens) ?? 0);
    }

    public string TotalOutputTokensLabel
    {
        get => FormatTokens(Payload?.History.Daily.Sum(d => (long)d.OutputTokens) ?? 0);
    }

    public string AvgCostPerDayLabel
    {
        get
        {
            var days = Payload?.History.Daily.Count ?? 0;
            if (days == 0) return "—";
            return $"${Payload!.Current.Cost / days:N2}";
        }
    }

    public string PeakDayCostLabel
    {
        get
        {
            var daily = Payload?.History.Daily;
            if (daily == null || daily.Count == 0) return "—";
            var peak = daily.OrderByDescending(d => d.Cost).First();
            if (!DateTime.TryParse(peak.Date, out var dt))
                return $"${peak.Cost:N2}";
            return $"${peak.Cost:N2} on {dt:MMM d}";
        }
    }

    // --- Commands ---

    [RelayCommand]
    private async Task SelectPeriod(string period)
    {
        if (SelectedPeriod == period) return;
        SelectedPeriod = period;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task SelectProvider(string provider)
    {
        if (SelectedProvider == provider) return;
        SelectedProvider = provider;
        await RefreshAsync();
    }

    [RelayCommand]
    private void SelectTab(string tab) => SelectedTab = tab;

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            Payload = await LocalDataClient.FetchAsync(SelectedPeriod, SelectedProvider);
            if (SelectedPeriod == "today")
                TodayPayload = Payload;
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    // --- Change propagation ---

    partial void OnLastErrorChanged(string? value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(HasData));
    }

    partial void OnPayloadChanged(MenubarPayload? value)
    {
        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(CostDisplay));
        OnPropertyChanged(nameof(CallsDisplay));
        OnPropertyChanged(nameof(SessionsDisplay));
        OnPropertyChanged(nameof(DayCount));
        OnPropertyChanged(nameof(TotalTokensLabel));
        OnPropertyChanged(nameof(AvgDailyTokensLabel));
        OnPropertyChanged(nameof(PeakTokensLabel));
        OnPropertyChanged(nameof(PeakDateLabel));
        OnPropertyChanged(nameof(YesterdayTokensLabel));
        OnPropertyChanged(nameof(ChartBars));
        OnPropertyChanged(nameof(ActivityBars));
        OnPropertyChanged(nameof(HasActivities));
        // Forecast
        OnPropertyChanged(nameof(ProjectedMonthlyCostLabel));
        OnPropertyChanged(nameof(DailyBurnRateLabel));
        OnPropertyChanged(nameof(MonthProgressLabel));
        OnPropertyChanged(nameof(MonthProgressWidth));
        // Pulse
        OnPropertyChanged(nameof(CostPerCallLabel));
        OnPropertyChanged(nameof(AvgCallsPerDayLabel));
        OnPropertyChanged(nameof(BusiestDayLabel));
        OnPropertyChanged(nameof(TokenRatioLabel));
        // Stats
        OnPropertyChanged(nameof(TotalInputTokensLabel));
        OnPropertyChanged(nameof(TotalOutputTokensLabel));
        OnPropertyChanged(nameof(AvgCostPerDayLabel));
        OnPropertyChanged(nameof(PeakDayCostLabel));
    }

    partial void OnSelectedPeriodChanged(string value)
    {
        OnPropertyChanged(nameof(IsTodayPeriod));
        OnPropertyChanged(nameof(Is7DPeriod));
        OnPropertyChanged(nameof(Is30DPeriod));
        OnPropertyChanged(nameof(IsMonthPeriod));
        OnPropertyChanged(nameof(IsAllPeriod));
        NotifyPeriodHistoryDependents();
    }

    private void NotifyPeriodHistoryDependents()
    {
        OnPropertyChanged(nameof(DayCount));
        OnPropertyChanged(nameof(TotalTokensLabel));
        OnPropertyChanged(nameof(AvgDailyTokensLabel));
        OnPropertyChanged(nameof(PeakTokensLabel));
        OnPropertyChanged(nameof(PeakDateLabel));
        OnPropertyChanged(nameof(ChartBars));
    }

    partial void OnSelectedProviderChanged(string value)
    {
        OnPropertyChanged(nameof(IsAllProvider));
        OnPropertyChanged(nameof(IsClaudeProvider));
        OnPropertyChanged(nameof(IsCodexProvider));
        OnPropertyChanged(nameof(IsCursorProvider));
        OnPropertyChanged(nameof(IsPilotProvider));
    }

    partial void OnSelectedTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsTrendTab));
        OnPropertyChanged(nameof(IsForecastTab));
        OnPropertyChanged(nameof(IsPulseTab));
        OnPropertyChanged(nameof(IsStatsTab));
    }

    private static string FormatTokens(long tokens)
    {
        if (tokens >= 1_000_000) return $"{tokens / 1_000_000.0:F1}M tok";
        if (tokens >= 1_000) return $"{tokens / 1_000.0:F0}K tok";
        return $"{tokens} tok";
    }
}
