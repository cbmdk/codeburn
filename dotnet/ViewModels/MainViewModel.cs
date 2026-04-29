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

    public int DayCount => Payload?.History.Daily.Count ?? 0;

    public string TotalTokensLabel
    {
        get
        {
            var total = Payload?.History.Daily.Sum(d => (long)(d.InputTokens + d.OutputTokens)) ?? 0;
            return FormatTokens(total);
        }
    }

    public string AvgDailyTokensLabel
    {
        get
        {
            var days = Payload?.History.Daily.Count ?? 0;
            if (days == 0) return "—";
            var total = Payload!.History.Daily.Sum(d => (long)(d.InputTokens + d.OutputTokens));
            return FormatTokens(total / days);
        }
    }

    public string PeakTokensLabel
    {
        get
        {
            if (Payload == null || Payload.History.Daily.Count == 0) return "—";
            var peak = Payload.History.Daily.Max(d => d.InputTokens + d.OutputTokens);
            return FormatTokens(peak);
        }
    }

    public string PeakDateLabel
    {
        get
        {
            if (Payload == null || Payload.History.Daily.Count == 0) return "";
            var entry = Payload.History.Daily
                .OrderByDescending(d => d.InputTokens + d.OutputTokens)
                .First();
            if (DateTime.TryParse(entry.Date, out var dt))
                return dt.ToString("MM/dd");
            return entry.Date;
        }
    }

    public string YesterdayTokensLabel
    {
        get
        {
            var daily = Payload?.History.Daily;
            if (daily == null || daily.Count < 2) return "—";
            var yesterday = daily[daily.Count - 2];
            return FormatTokens(yesterday.InputTokens + yesterday.OutputTokens);
        }
    }

    public IReadOnlyList<ChartBarItem> ChartBars
    {
        get
        {
            var daily = Payload?.History.Daily;
            if (daily == null || daily.Count == 0) return [];
            var max = daily.Max(d => d.InputTokens + d.OutputTokens);
            if (max == 0) return [];
            const double maxH = 80;
            var peakIdx = daily
                .Select((d, i) => (d, i))
                .OrderByDescending(x => x.d.InputTokens + x.d.OutputTokens)
                .First().i;
            return daily.Select((d, i) => new ChartBarItem
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
            Payload = await DataClient.FetchAsync(SelectedPeriod, SelectedProvider);
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
    }

    partial void OnSelectedPeriodChanged(string value)
    {
        OnPropertyChanged(nameof(IsTodayPeriod));
        OnPropertyChanged(nameof(Is7DPeriod));
        OnPropertyChanged(nameof(Is30DPeriod));
        OnPropertyChanged(nameof(IsMonthPeriod));
        OnPropertyChanged(nameof(IsAllPeriod));
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
