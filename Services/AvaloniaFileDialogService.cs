using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;

namespace HouseDesigner.Services;

public sealed class AvaloniaFileDialogService(Window owner) : IFileDialogService
{
    public async Task<string?> SaveFileAsync(string title, string suggestedFileName, string extension, string typeName,
        string initialDirectory)
    {
        var startFolder = await owner.StorageProvider.TryGetFolderFromPathAsync(new Uri(initialDirectory));
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = extension.TrimStart('.'),
            ShowOverwritePrompt = true,
            SuggestedStartLocation = startFolder,
            FileTypeChoices = [CreateFileType(typeName, extension)]
        });

        return GetLocalPath(file);
    }

    public async Task<string?> OpenFileAsync(string title, string extension, string typeName, string initialDirectory)
    {
        var startFolder = await owner.StorageProvider.TryGetFolderFromPathAsync(new Uri(initialDirectory));
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = startFolder,
            FileTypeFilter = [CreateFileType(typeName, extension), FilePickerFileTypes.All]
        });

        return files.Count == 0 ? null : GetLocalPath(files[0]);
    }

    public async Task ShowErrorAsync(string message, Exception exception)
    {
        var closeButton = new Button
        {
            Content = "확인",
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 84
        };
        var dialog = new Window
        {
            Title = "House Designer",
            Width = 460,
            Height = 210,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(24),
                Spacing = 18,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"{message}\n\n{exception.Message}",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    closeButton
                }
            }
        };
        closeButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(owner);
    }

    private static FilePickerFileType CreateFileType(string name, string extension) => new(name)
    {
        Patterns = [$"*.{extension.TrimStart('.')}" ]
    };

    private static string? GetLocalPath(IStorageItem? item) =>
        item?.Path.IsFile == true ? item.Path.LocalPath : null;
}
