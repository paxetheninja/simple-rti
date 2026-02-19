using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimpleRti.Ptm;
using SimpleRti.Renderer;

namespace SimpleRti.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private PtmFile? _ptmFile;

    [ObservableProperty]
    private float _lightU;

    [ObservableProperty]
    private float _lightV;

    [ObservableProperty]
    private RenderMode _renderMode;

    [ObservableProperty]
    private float _specularExponent = 32f;

    [ObservableProperty]
    private float _diffuseGain = 1f;

    [ObservableProperty]
    private string _statusText = "No file loaded. Use File > Open to load a PTM file.";

    [ObservableProperty]
    private string _windowTitle = "Simple RTI Viewer";

    [ObservableProperty]
    private bool _hasFile;

    private Window? _window;

    public void SetWindow(Window window)
    {
        _window = window;
    }

    [RelayCommand]
    private async Task OpenFile()
    {
        if (_window == null) return;

        var storageProvider = _window.StorageProvider;
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open PTM File",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("PTM Files") { Patterns = ["*.ptm"] },
                new FilePickerFileType("All Files") { Patterns = ["*.*"] }
            ]
        });

        if (files.Count == 0) return;

        var file = files[0];
        await LoadFile(file.Path.LocalPath, file.Name);
    }

    public async Task LoadFile(string path, string displayName)
    {
        try
        {
            StatusText = "Loading...";

            PtmFile = await Task.Run(() => PtmReader.Read(path));

            HasFile = true;
            WindowTitle = $"Simple RTI Viewer - {displayName}";
            StatusText = $"{displayName} | {PtmFile.Header.Width}x{PtmFile.Header.Height} | {PtmFile.Header.Format}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            HasFile = false;
        }
    }
}
