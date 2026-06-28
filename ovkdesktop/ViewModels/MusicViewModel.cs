using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ovkdesktop.Models;
using ovkdesktop.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using ovkdesktop.Helpers;

namespace ovkdesktop.ViewModels
{
    public partial class MusicViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        private readonly IAPIServiceMusic _musicService;
        private readonly AudioPlayerService _audioPlayerService;

        [ObservableProperty]
        private ObservableCollection<Audio> _myAudioCollection = new();

        [ObservableProperty]
        private ObservableCollection<Audio> _popularAudioCollection = new();

        [ObservableProperty]
        private ObservableCollection<Audio> _recommendedAudioCollection = new();

        [ObservableProperty]
        private bool _isLoadingMyAudio;

        [ObservableProperty]
        private bool _isLoadingPopularAudio;

        [ObservableProperty]
        private bool _isLoadingSearchAudio;

        [ObservableProperty]
        private bool _isGlobalLoading;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private string _myAudioHeaderText = "Мои аудиозаписи";

        [ObservableProperty]
        private string _searchHeaderText = "Рекомендации";

        private enum AudioMode { Popular, MyAudio, Search }
        private AudioMode _currentMode = AudioMode.Popular;

        private readonly ovkdesktop.Services.Interfaces.IDispatcherService _dispatcherService;

        public MusicViewModel(IAPIServiceMusic musicService, AudioPlayerService audioPlayerService, ovkdesktop.Services.Interfaces.IDispatcherService dispatcherService)
        {
            _musicService = musicService;
            _audioPlayerService = audioPlayerService;
            _dispatcherService = dispatcherService;

            if (_audioPlayerService != null)
            {
                _audioPlayerService.FavoriteStatusChanged += AudioService_FavoriteStatusChanged;
            }
        }

        [RelayCommand]
        public async Task LoadPopularAudioAsync()
        {
            try
            {
                IsGlobalLoading = true;
                IsLoadingPopularAudio = true;
                _currentMode = AudioMode.Popular;

                string token = await SessionHelper.GetTokenAsync();
                if (string.IsNullOrEmpty(token)) return;

                var audios = await _musicService.GetPopularAudioAsync(token);
                PopularAudioCollection.Clear();
                foreach (var audio in audios)
                {
                    PopularAudioCollection.Add(audio);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MusicViewModel] Error loading popular audio: {ex.Message}");
            }
            finally
            {
                IsLoadingPopularAudio = false;
                IsGlobalLoading = false;
            }
        }

        [RelayCommand]
        public async Task LoadMyAudioAsync()
        {
            try
            {
                IsGlobalLoading = true;
                IsLoadingMyAudio = true;
                _currentMode = AudioMode.MyAudio;

                var favoriteAudios = await _audioPlayerService.GetFavoriteAudioAsync();
                MyAudioCollection.Clear();

                if (favoriteAudios != null && favoriteAudios.Count > 0)
                {
                    foreach (var audio in favoriteAudios)
                    {
                        MyAudioCollection.Add(audio);
                    }
                    MyAudioHeaderText = $"Мои треки ({MyAudioCollection.Count})";
                }
                else
                {
                    MyAudioHeaderText = "You have no saved audios";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MusicViewModel] Error loading my audio: {ex.Message}");
            }
            finally
            {
                IsLoadingMyAudio = false;
                IsGlobalLoading = false;
            }
        }

        [RelayCommand]
        public async Task LoadRecommendedAudioAsync()
        {
            try
            {
                IsLoadingSearchAudio = true;
                _currentMode = AudioMode.Search;

                string token = await SessionHelper.GetTokenAsync();
                if (string.IsNullOrEmpty(token)) return;

                var audios = await _musicService.GetRecommendedAudioAsync(token);
                RecommendedAudioCollection.Clear();

                foreach (var audio in audios)
                {
                    RecommendedAudioCollection.Add(audio);
                }
                SearchHeaderText = "Рекомендации";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MusicViewModel] Error loading recommended audio: {ex.Message}");
            }
            finally
            {
                IsLoadingSearchAudio = false;
            }
        }

        [RelayCommand]
        public async Task SearchAudioAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery)) return;

            try
            {
                IsGlobalLoading = true;
                IsLoadingSearchAudio = true;
                _currentMode = AudioMode.Search;

                string token = await SessionHelper.GetTokenAsync();
                if (string.IsNullOrEmpty(token)) return;

                var audios = await _musicService.SearchAudioAsync(token, SearchQuery.Trim());
                RecommendedAudioCollection.Clear();

                foreach (var audio in audios)
                {
                    RecommendedAudioCollection.Add(audio);
                }

                SearchHeaderText = audios.Count > 0 ? $"Поиск: {SearchQuery} (найдено: {audios.Count})" : $"Поиск: {SearchQuery} (нет результатов)";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MusicViewModel] Error searching audio: {ex.Message}");
            }
            finally
            {
                IsLoadingSearchAudio = false;
                IsGlobalLoading = false;
            }
        }

        [RelayCommand]
        public async Task ToggleFavoriteAsync(Audio audio)
        {
            if (audio == null) return;

            bool wasAdded = audio.IsAdded;
            audio.IsAdded = !wasAdded;

            // Optimistic UI update for 'My Audio' list
            if (_currentMode == AudioMode.MyAudio && !audio.IsAdded)
            {
                MyAudioCollection.Remove(audio);
                MyAudioHeaderText = $"Мои треки ({MyAudioCollection.Count})";
            }

            bool success;
            if (wasAdded)
            {
                success = await _audioPlayerService.RemoveFromFavorites(audio);
            }
            else
            {
                success = await _audioPlayerService.AddToFavorites(audio);
            }

            if (!success)
            {
                audio.IsAdded = wasAdded;
                if (_currentMode == AudioMode.MyAudio && wasAdded)
                {
                    if (!MyAudioCollection.Contains(audio))
                    {
                        MyAudioCollection.Add(audio);
                        MyAudioHeaderText = $"Мои треки ({MyAudioCollection.Count})";
                    }
                }
            }
            else
            {
                if (_audioPlayerService.CurrentAudio != null &&
                    _audioPlayerService.CurrentAudio.Id == audio.Id &&
                    _audioPlayerService.CurrentAudio.OwnerId == audio.OwnerId)
                {
                    _audioPlayerService.CurrentAudio.IsAdded = audio.IsAdded;
                }
            }
        }

        [RelayCommand]
        public void PlayAudio(Audio audio)
        {
            if (audio == null) return;

            ObservableCollection<Audio> currentPlaylist = _currentMode switch
            {
                AudioMode.MyAudio => MyAudioCollection,
                AudioMode.Popular => PopularAudioCollection,
                AudioMode.Search => RecommendedAudioCollection,
                _ => PopularAudioCollection,
            };

            if (currentPlaylist == null || currentPlaylist.Count == 0)
            {
                _audioPlayerService.SetPlaylist(new ObservableCollection<Audio> { audio }, 0);
                return;
            }

            int index = currentPlaylist.IndexOf(audio);
            if (index == -1)
            {
                // Try finding by Id and OwnerId if reference equality fails
                for (int i = 0; i < currentPlaylist.Count; i++)
                {
                    if (currentPlaylist[i].Id == audio.Id && currentPlaylist[i].OwnerId == audio.OwnerId)
                    {
                        index = i;
                        break;
                    }
                }
            }

            if (index >= 0)
            {
                _audioPlayerService.SetPlaylist(currentPlaylist, index);
            }
            else
            {
                _audioPlayerService.SetPlaylist(new ObservableCollection<Audio> { audio }, 0);
            }
        }

        [RelayCommand]
        public void Refresh()
        {
            switch (_currentMode)
            {
                case AudioMode.MyAudio:
                    LoadMyAudioCommand.Execute(null);
                    break;
                case AudioMode.Popular:
                    LoadPopularAudioCommand.Execute(null);
                    break;
                case AudioMode.Search:
                    if (!string.IsNullOrWhiteSpace(SearchQuery))
                    {
                        SearchAudioCommand.Execute(null);
                    }
                    else
                    {
                        LoadRecommendedAudioCommand.Execute(null);
                    }
                    break;
            }
        }

        public void ChangeMode(int pivotIndex)
        {
            switch (pivotIndex)
            {
                case 0:
                    _currentMode = AudioMode.MyAudio;
                    if (MyAudioCollection.Count == 0) LoadMyAudioCommand.Execute(null);
                    break;
                case 1:
                    _currentMode = AudioMode.Popular;
                    if (PopularAudioCollection.Count == 0) LoadPopularAudioCommand.Execute(null);
                    break;
                case 2:
                    _currentMode = AudioMode.Search;
                    if (!string.IsNullOrWhiteSpace(SearchQuery) && RecommendedAudioCollection.Count > 0)
                    {
                        // Already showing search results
                    }
                    else if (RecommendedAudioCollection.Count == 0)
                    {
                        LoadRecommendedAudioCommand.Execute(null);
                    }
                    break;
            }
        }

        private void AudioService_FavoriteStatusChanged(object sender, Audio audio)
        {
            // Sync with UI from service events
            if (_currentMode == AudioMode.MyAudio)
            {
                _dispatcherService.TryEnqueue(() =>
                {
                    if (!audio.IsAdded)
                    {
                        for (int i = MyAudioCollection.Count - 1; i >= 0; i--)
                        {
                            if (MyAudioCollection[i].Id == audio.Id && MyAudioCollection[i].OwnerId == audio.OwnerId)
                            {
                                MyAudioCollection.RemoveAt(i);
                            }
                        }
                    }
                    else
                    {
                        bool exists = false;
                        foreach (var a in MyAudioCollection)
                        {
                            if (a.Id == audio.Id && a.OwnerId == audio.OwnerId)
                            {
                                exists = true;
                                break;
                            }
                        }
                        if (!exists)
                        {
                            MyAudioCollection.Add(audio);
                        }
                    }
                    MyAudioHeaderText = MyAudioCollection.Count > 0 ? $"Мои треки ({MyAudioCollection.Count})" : "You have no saved audios";
                });
            }
        }

        public void Dispose()
        {
            if (_audioPlayerService != null)
            {
                _audioPlayerService.FavoriteStatusChanged -= AudioService_FavoriteStatusChanged;
            }
        }
    }
}
