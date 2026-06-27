using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ovkdesktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ovkdesktop
{
    public sealed partial class AuthPage : Page
    {
        public AuthViewModel ViewModel { get; }

        public AuthPage()
        {
            this.InitializeComponent();
            ViewModel = ((App)Microsoft.UI.Xaml.Application.Current).Services.GetRequiredService<AuthViewModel>();
            this.DataContext = ViewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            
            string url = e.Parameter as string;
            await ViewModel.InitializeAsync(url);
        }
    }
}
