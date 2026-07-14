using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Runtime.InteropServices;

namespace ovkdesktop.Helpers
{
    /// <summary>
    /// Restores a ScrollViewer only after its content has been measured.
    /// A single dispatcher callback is not enough for virtualized lists.
    /// </summary>
    public sealed class ScrollPositionKeeper
    {
        private ScrollViewer _viewer;
        private double _targetOffset;
        private bool _isPending;
        private int _layoutAttempts;

        public double SavedOffset { get; private set; }

        public void Save(ScrollViewer viewer)
        {
            if (viewer != null)
                SavedOffset = viewer.VerticalOffset;
        }

        public void Restore(ScrollViewer viewer) => Restore(viewer, SavedOffset);

        public void Restore(ScrollViewer viewer, double offset)
        {
            CancelPendingRestore();

            if (viewer == null || offset <= 0)
                return;

            _viewer = viewer;
            _targetOffset = offset;
            _isPending = true;
            _layoutAttempts = 0;
            _viewer.Loaded += Viewer_Loaded;
            _viewer.LayoutUpdated += Viewer_LayoutUpdated;
            _viewer.DispatcherQueue.TryEnqueue(TryRestore);
        }

        public void CancelPendingRestore()
        {
            if (_viewer != null)
            {
                _viewer.Loaded -= Viewer_Loaded;
                _viewer.LayoutUpdated -= Viewer_LayoutUpdated;
            }

            _viewer = null;
            _isPending = false;
        }

        private void Viewer_LayoutUpdated(object sender, object e) => TryRestore();
        private void Viewer_Loaded(object sender, RoutedEventArgs e) => TryRestore();

        private void TryRestore()
        {
            if (!_isPending || _viewer == null || !_viewer.IsLoaded || _viewer.XamlRoot == null)
                return;

            try
            {
                _layoutAttempts++;
                double availableOffset = _viewer.ScrollableHeight;
                if (availableOffset + 1 < _targetOffset && _layoutAttempts < 30)
                    return;

                double target = Math.Min(_targetOffset, availableOffset);
                _viewer.ChangeView(null, target, null, true);
                CancelPendingRestore();
            }
            catch (COMException)
            {
                // WinUI can report E_UNEXPECTED while Frame.GoBack is rebuilding
                // the visual tree. Keep the request pending for the next layout.
            }
        }
    }
}
