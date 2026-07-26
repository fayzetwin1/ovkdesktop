using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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

            // put the caret where the user still has something to type
            if (string.IsNullOrEmpty(ViewModel.Username))
                usernameTextBox.Focus(FocusState.Programmatic);
            else
                passwordTextBox.Focus(FocusState.Programmatic);
        }

        private void PasswordBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != Windows.System.VirtualKey.Enter)
                return;

            e.Handled = true;
            if (ViewModel.LoginCommand.CanExecute(null))
                ViewModel.LoginCommand.Execute(null);
        }
    }
}
