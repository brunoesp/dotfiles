using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MediaLog.Services;
using MediaLog.ViewModels;
using MediaLog.Views;

namespace MediaLog;

public partial class App : Application
{
    private MainViewModel? _viewModel;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    // One view model per process, so Android activity re-creation keeps the state.
    private MainViewModel ViewModel => _viewModel ??= new MainViewModel(new LibraryStore());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow { DataContext = ViewModel };
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime activity)
        {
            activity.MainViewFactory = () => new MainView { DataContext = ViewModel };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = new MainView { DataContext = ViewModel };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
