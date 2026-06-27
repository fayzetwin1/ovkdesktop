using Microsoft.UI.Xaml.Controls;
using ovkdesktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ovkdesktop
{
    public sealed partial class WelcomePage : Page
    {
        public WelcomeViewModel ViewModel { get; }

        public WelcomePage()
        {
            this.InitializeComponent();
            ViewModel = ((App)Microsoft.UI.Xaml.Application.Current).Services.GetRequiredService<WelcomeViewModel>();
            this.DataContext = ViewModel;
        }
    }
}
