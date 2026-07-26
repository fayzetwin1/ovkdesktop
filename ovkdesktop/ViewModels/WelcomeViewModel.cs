using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ovkdesktop.Services;
using ovkdesktop.Models;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ovkdesktop.ViewModels
{
    public partial class WelcomeViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        private readonly INavigationService _navigationService;
        private readonly IDialogService _dialogService;
        
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(InstanceDisplayName))]
        private string instanceUrl = "https://api.openvk.org/";

        /// <summary>host of the selected instance, for showing on the welcome screen</summary>
        public string InstanceDisplayName =>
            Uri.TryCreate(InstanceUrl, UriKind.Absolute, out var uri) ? uri.Host : InstanceUrl;

        public WelcomeViewModel(INavigationService navigationService, IDialogService dialogService)
        {
            _navigationService = navigationService;
            _dialogService = dialogService;
            
            _ = LoadInstanceUrlAsync();
        }

        private async Task LoadInstanceUrlAsync()
        {
            try
            {
                var settings = await AppSettings.LoadAsync();
                InstanceUrl = settings.InstanceUrl;
                Debug.WriteLine($"[WelcomeViewModel] Loaded URL instance from ovkcfg.json: {InstanceUrl}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WelcomeViewModel] Error when processing URL instance: {ex.Message}");
            }
        }

        [RelayCommand]
        private void NavigateToAuth()
        {
            _navigationService.NavigateTo(typeof(AuthPage), InstanceUrl);
        }

        [RelayCommand]
        private async Task ChangeInstanceAsync()
        {
            var selectedInstance = await _dialogService.ShowInstanceSelectionDialogAsync(InstanceUrl);
            if (selectedInstance != null)
            {
                InstanceUrl = selectedInstance;
                
                try
                {
                    var settings = await AppSettings.LoadAsync();
                    settings.InstanceUrl = InstanceUrl;
                    await settings.SaveAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WelcomeViewModel] Error saving instance url: {ex.Message}");
                }

                // stay on the welcome screen: picking an instance is a setting, not a
                // decision to sign in, and jumping straight to the login form was jarring
            }
        }
    }
}
