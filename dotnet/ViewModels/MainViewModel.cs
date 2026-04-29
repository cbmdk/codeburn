using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Collections.Generic;
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
    [ObservableProperty]
    private string selectedProvider = "all";

    [ObservableProperty]
    private string selectedPeriod = "today";

    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private string? lastError;

    [ObservableProperty]
    private MenubarPayload? payload;

    [ObservableProperty]
    private MenubarPayload? todayPayload;

    public bool HasError => !string.IsNullOrEmpty(LastError);
    public bool HasData => Payload != null && !HasError;

    public IRelayCommand? CloseCommand { get; set; }

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
}