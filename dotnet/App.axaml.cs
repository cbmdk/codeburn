using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using CodeBurnMenubar.ViewModels;
using CodeBurnMenubar.Views;

namespace CodeBurnMenubar;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Line below is needed to remove Avalonia data validation.
            // Without this line you will get duplicate validations from both Avalonia and the CommunityToolkit.
            // BindingPlugins.DataValidators.RemoveAt(0); // Commented out as it's inaccessible

            var viewModel = new MainViewModel();
            var trayIcon = new TrayIcon
            {
                Icon = new WindowIcon("Assets/flame.ico"), // TODO: Add icon
                ToolTipText = "CodeBurn",
                Menu = new NativeMenu()
                {
                    Items =
                    {
                        new NativeMenuItem("Show") { Command = new RelayCommand(() => ShowPopover(viewModel)) },
                        new NativeMenuItemSeparator(),
                        new NativeMenuItem("Exit") { Command = new RelayCommand(() => desktop.Shutdown()) }
                    }
                }
            };

            trayIcon.Clicked += (s, e) => ShowPopover(viewModel);

            var trayIcons = new TrayIcons();
            trayIcons.Add(trayIcon);

            TrayIcon.SetIcons(this, trayIcons);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private MainWindow? _popover;

    private void ShowPopover(MainViewModel viewModel)
    {
        if (_popover == null)
            _popover = new MainWindow { DataContext = viewModel };

        _ = viewModel.RefreshAsync();
        _popover.Show();
        _popover.Activate();
    }
}