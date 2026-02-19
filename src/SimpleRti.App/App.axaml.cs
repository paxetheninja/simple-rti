using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SimpleRti.App.ViewModels;

namespace SimpleRti.App;

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
            var window = new MainWindow();
            desktop.MainWindow = window;

            // Load file from command line argument
            var args = desktop.Args;
            if (args is { Length: > 0 })
            {
                var filePath = args[0];
                if (File.Exists(filePath) &&
                    filePath.EndsWith(".ptm", StringComparison.OrdinalIgnoreCase))
                {
                    var vm = (MainViewModel)window.DataContext!;
                    _ = vm.LoadFile(filePath, Path.GetFileName(filePath));
                }
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
