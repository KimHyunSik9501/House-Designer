using System.Windows;
using HouseDesigner.ViewModels;

namespace HouseDesigner;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
