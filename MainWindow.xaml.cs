using Avalonia.Controls;
using HouseDesigner.Services;
using HouseDesigner.ViewModels;

namespace HouseDesigner;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(new AvaloniaFileDialogService(this));
    }
}
