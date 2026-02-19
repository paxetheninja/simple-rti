using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SimpleRti.App.ViewModels;
using SimpleRti.Renderer;

namespace SimpleRti.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var vm = (MainViewModel)DataContext!;
        vm.SetWindow(this);

        // When PtmFile changes, load it into the GL control
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.PtmFile) && vm.PtmFile != null)
            {
                GlControl.LoadPtm(vm.PtmFile);
            }
        };

        // Enable drag and drop
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        DragDrop.SetAllowDrop(this, true);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var files = e.Data.GetFiles()?.ToList();
        if (files == null || files.Count == 0) return;

        var file = files[0];
        var path = file.Path.LocalPath;
        if (path.EndsWith(".ptm", StringComparison.OrdinalIgnoreCase))
        {
            await vm.LoadFile(path, file.Name);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (DataContext is not MainViewModel vm) return;

        const float step = 0.05f;
        switch (e.Key)
        {
            case Key.Left:
                vm.LightU = Math.Max(-1f, vm.LightU - step);
                e.Handled = true;
                break;
            case Key.Right:
                vm.LightU = Math.Min(1f, vm.LightU + step);
                e.Handled = true;
                break;
            case Key.Up:
                vm.LightV = Math.Min(1f, vm.LightV + step);
                e.Handled = true;
                break;
            case Key.Down:
                vm.LightV = Math.Max(-1f, vm.LightV - step);
                e.Handled = true;
                break;
        }
    }

    private void OnExitClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Guard: this fires during XAML init before named controls are resolved
        if (DataContext is not MainViewModel vm || SpecularLabel == null) return;
        if (ModeComboBox.SelectedIndex < 0) return;

        vm.RenderMode = (RenderMode)ModeComboBox.SelectedIndex;

        SpecularLabel.IsVisible = vm.RenderMode == RenderMode.SpecularEnhancement;
        SpecularSlider.IsVisible = vm.RenderMode == RenderMode.SpecularEnhancement;
        GainLabel.IsVisible = vm.RenderMode == RenderMode.DiffuseGain;
        GainSlider.IsVisible = vm.RenderMode == RenderMode.DiffuseGain;
    }
}
