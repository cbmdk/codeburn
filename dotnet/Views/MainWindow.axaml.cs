using Avalonia.Controls;
using CodeBurnMenubar.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CodeBurnMenubar.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        var vm = (MainViewModel)DataContext!;
        vm.CloseCommand = new RelayCommand(Close);
    }
}