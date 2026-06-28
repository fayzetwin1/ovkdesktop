using System.Threading.Tasks;

namespace ovkdesktop.Services.Interfaces
{
    public interface IClipboardService
    {
        Task<bool> CopyImageToClipboardAsync(string imageUrl);
    }
}
