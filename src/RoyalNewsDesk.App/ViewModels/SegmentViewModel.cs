using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using RoyalNewsDesk.Core.Models;

namespace RoyalNewsDesk.App.ViewModels;

public partial class SegmentViewModel : ObservableObject
{
    private readonly string _imagesDir;

    [ObservableProperty]
    private string? _headline;

    [ObservableProperty]
    private string _body = "";

    [ObservableProperty]
    private string? _imageFile;

    public SegmentViewModel(Segment segment, string imagesDir)
    {
        _imagesDir = imagesDir;
        Id = segment.Id;
        Headline = segment.Headline;
        Body = segment.Body;
        ImageFile = segment.ImageFile;
    }

    public string Id { get; }

    public bool HasImage => !string.IsNullOrEmpty(ImageFile);

    public Segment ToModel() => new()
    {
        Id = Id,
        Headline = string.IsNullOrWhiteSpace(Headline) ? null : Headline!.Trim(),
        Body = Body,
        ImageFile = ImageFile,
    };

    partial void OnImageFileChanged(string? value) => OnPropertyChanged(nameof(HasImage));

    [RelayCommand]
    private void PickImage()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Images|*.jpg;*.jpeg;*.png;*.webp;*.bmp",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        Directory.CreateDirectory(_imagesDir);
        var targetName = Id + Path.GetExtension(dialog.FileName).ToLowerInvariant();
        var targetPath = Path.Combine(_imagesDir, targetName);
        File.Copy(dialog.FileName, targetPath, overwrite: true);
        ImageFile = targetName;
    }

    [RelayCommand]
    private void RemoveImage()
    {
        if (ImageFile is { } name)
        {
            var path = Path.Combine(_imagesDir, name);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        ImageFile = null;
    }
}
