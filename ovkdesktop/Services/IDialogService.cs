using System.Threading.Tasks;

namespace ovkdesktop.Services
{
    public interface IDialogService
    {
        Task ShowMessageAsync(string title, string content, string primaryButtonText = "OK");
        Task<string> ShowInstanceSelectionDialogAsync(string currentInstanceUrl);
        Task<string> Show2FAInputDialogAsync();
    }
}
