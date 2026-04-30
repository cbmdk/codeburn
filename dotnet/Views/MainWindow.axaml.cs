using System;
using Avalonia.Controls;
using CodeBurnMenubar.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CodeBurnMenubar.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.CloseCommand = new RelayCommand(Hide);
        }
    }
}