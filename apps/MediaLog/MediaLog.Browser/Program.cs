using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using MediaLog;
using MediaLog.Browser;

internal sealed partial class Program
{
    private static Task Main(string[] args)
    {
        App.Storage = new LocalStorageLibraryStorage();
        return BuildAvaloniaApp()
            .WithInterFont()
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();
}
