namespace HouseDesigner.Services;

public interface IFileDialogService
{
    Task<string?> SaveFileAsync(string title, string suggestedFileName, string extension, string typeName,
        string initialDirectory);

    Task<string?> OpenFileAsync(string title, string extension, string typeName, string initialDirectory);

    Task ShowErrorAsync(string message, Exception exception);
}
