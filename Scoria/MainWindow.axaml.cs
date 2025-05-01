using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Scoria.Services;
using Scoria.Rendering;
using Scoria.ViewModels;

namespace Scoria.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // 1) Manually inject our services and renderer into the VM
            DataContext = new MainWindowViewModel(
                new FileExplorerService(),
                new ToastService(RootPanel),
                new MarkdownRenderer());
        }

        private async void OpenFolder_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var vm  = (MainWindowViewModel)DataContext!;
            var dlg = new OpenFolderDialog { Title = "Select a vault / folder" };
            var path = await dlg.ShowAsync(this);
            if (!string.IsNullOrWhiteSpace(path))
                vm.LoadFolder(path);              // simple helper on the VM
        }
        
        // 2) Keep window dragging on the transparent title‐bar
        private void TitleBar_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            BeginMoveDrag(e);
        }
    }
}