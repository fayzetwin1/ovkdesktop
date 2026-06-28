using System.Collections.Generic;
using System.Threading.Tasks;

namespace ovkdesktop.Services.Interfaces
{
    public interface IFilePickerService
    {
        Task<string> PickSingleFileAsync(string[] extensions);
        Task<IReadOnlyList<string>> PickMultipleFilesAsync(string[] extensions);
        Task<string> PickSaveFileAsync(string suggestedFileName, string extensionName, string[] extensions);
    }
}
