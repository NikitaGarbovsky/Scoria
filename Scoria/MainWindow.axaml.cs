using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Scoria.Services;
using Scoria.Rendering;
using Scoria.ViewModels;

namespace Scoria.Views
{
    /// <summary>
    /// Code-behind for <c>MainWindow.axaml</c>.
    /// <para/>
    /// Primary duties:
    /// <list type="bullet">
    ///   <item>Instantiate and wire the <see cref="MainWindowViewModel"/> with
    ///         concrete service objects.</item>
    ///   <item>Handle the “Open Folder” toolbar button and forward the chosen
    ///         path to the view-model.</item>
    ///   <item>Provide a draggable, custom title-bar area.</item>
    /// </list>
    /// </summary>
    public partial class MainWindow : Window
    {
        /*──────────────────────────── 1.  Construction ───────────────────────────*/

        /// <summary>
        ///     Builds the visual tree (via <c>InitializeComponent()</c>),
        ///     then injects a freshly-constructed <see cref="MainWindowViewModel"/>
        ///     as <see cref="Window.DataContext"/>.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // Create the viewmodel and insert the appropriate objects as params 
            DataContext = new MainWindowViewModel(
                new FileExplorerService(),              // disk → FileItem tree
                new ToastService(RootPanel),            // in-window toast overlay
                new MarkdownRenderer());                // markdown → visuals
        }

        /*──────────────────────────── 2.  Toolbar Buttons ──────────────────*/
        
        /// <summary>
        /// Opens a system folder-picker, then calls
        /// <see cref="MainWindowViewModel.LoadFolder(string)"/> on the VM.
        /// </summary>
        private async void OpenFolder_Click(object? _sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var vm  = (MainWindowViewModel)DataContext!;
            var dlg = new OpenFolderDialog { Title = "Select a vault / folder" };
            
            string? path = await dlg.ShowAsync(this);
            
            if (!string.IsNullOrWhiteSpace(path))
                vm.LoadFolder(path);              // simple helper on the VM
        }
        
        /// <summary>
        /// Creates a blank “New Note.md” **in the directory that’s currently
        /// selected** in the tree.  
        ///     – Disabled automatically while no folder is loaded  
        ///     – No dialogs / blocking UI
        /// </summary>
        private void CreateNote_Click(object? _sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var vm = (MainWindowViewModel)DataContext!;
            if (!vm.IsFolderOpen) // safety-guard
                return;
            
            // Execute the create note functionality in the view model.            
            vm.CreateNote();                
        }
        /*──────────────────────────── 3.  Custom title-bar dragging ───────────────*/
        
        /// <summary>
        /// Allows users to drag the window by pressing on the transparent,
        /// custom title-bar row (see XAML).  Without this, the OS would not
        /// recognise the hit-test region as draggable.
        /// </summary>
        private void TitleBar_PointerPressed(object _sender, PointerPressedEventArgs _e)
        {
            BeginMoveDrag(_e);
        }
    }
}