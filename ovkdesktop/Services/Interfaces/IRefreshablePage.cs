using System.Threading.Tasks;

namespace ovkdesktop.Services.Interfaces
{
    /// <summary>
    /// implemented by pages that can reload their content, so that the shell can
    /// wire up F5 / Ctrl+R without knowing anything about the individual pages
    /// </summary>
    public interface IRefreshablePage
    {
        Task RefreshAsync();
    }
}
