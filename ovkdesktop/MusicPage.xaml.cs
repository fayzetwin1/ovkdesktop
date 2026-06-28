using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ovkdesktop.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace ovkdesktop
{
    public sealed partial class MusicPage : Page
    {
        public MusicViewModel ViewModel { get; }

        public MusicPage()
        {
            ViewModel = Ioc.Default.GetRequiredService<MusicViewModel>();
            this.InitializeComponent();
            
            this.Loaded += (s, e) => {
                if (ViewModel.PopularAudioCollection.Count == 0)
                {
                    ViewModel.LoadPopularAudioCommand.Execute(null);
                }
            };
            this.Unloaded += (s, e) => ViewModel.Dispose();
        }

        private void AudioPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ViewModel.ChangeMode(AudioPivot.SelectedIndex);
        }

        private void SearchBoxInTab_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                ViewModel.SearchAudioCommand.Execute(null);
            }
        }

        private void PlayAudio(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Models.Audio audio)
            {
                ViewModel.PlayAudioCommand.Execute(audio);
            }
        }

        private void FavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is Models.Audio audio)
            {
                ViewModel.ToggleFavoriteCommand.Execute(audio);
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.RefreshCommand.Execute(null);
        }
    }
}
