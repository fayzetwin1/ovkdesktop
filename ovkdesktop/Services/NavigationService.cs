using Microsoft.UI.Xaml.Controls;
using System;
using ovkdesktop.Services;

namespace ovkdesktop.Services
{
    public class NavigationService : INavigationService
    {
        private Frame _frame;

        public void Initialize(Frame frame)
        {
            _frame = frame;
        }

        public void NavigateTo(Type pageType, object parameter = null)
        {
            if (_frame != null)
            {
                if (parameter != null)
                    _frame.Navigate(pageType, parameter);
                else
                    _frame.Navigate(pageType);
            }
        }

        public void GoBack()
        {
            if (CanGoBack)
            {
                _frame.GoBack();
            }
        }

        public bool CanGoBack => _frame?.CanGoBack ?? false;
    }
}
