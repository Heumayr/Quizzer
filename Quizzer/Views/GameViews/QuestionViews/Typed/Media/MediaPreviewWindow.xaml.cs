using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Quizzer.Views.GameViews.QuestionViews.Typed.Media
{
    /// <summary>
    /// Interaction logic for MediaPreviewWindow.xaml
    /// </summary>
    public partial class MediaPreviewWindow : WindowBase
    {
        public Guid GroupId { get; }
        public string FilePath { get; }
        public ResourceType ResourceType { get; }
        public bool IsControlWindow { get; }

        public MediaPreviewWindow(Guid groupId, string filePath, ResourceType resourceType, bool isControlWindow)
        {
            InitializeComponent();

            GroupId = groupId;
            FilePath = filePath;
            ResourceType = resourceType;
            IsControlWindow = isControlWindow;

            Loaded += MediaPreviewWindow_Loaded;
            Closed += MediaPreviewWindow_Closed;
        }

        private void MediaPreviewWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ControlBar.Visibility = IsControlWindow
                ? Visibility.Visible
                : Visibility.Collapsed;

            switch (ResourceType)
            {
                case ResourceType.Image:
                    {
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.UriSource = new Uri(FilePath, UriKind.Absolute);
                        image.EndInit();
                        image.Freeze();

                        ImageViewer.Source = image;
                        ImageViewer.Visibility = Visibility.Visible;
                        break;
                    }

                case ResourceType.Video:
                    {
                        VideoViewer.Source = new Uri(FilePath, UriKind.Absolute);
                        VideoViewer.Visibility = Visibility.Visible;
                        VideoViewer.Play();
                        break;
                    }
            }

            Focus();
            Activate();
        }

        private void MediaPreviewWindow_Closed(object? sender, EventArgs e)
        {
            try
            {
                VideoViewer.Stop();
                VideoViewer.Source = null;
            }
            catch
            {
            }
        }

        public void StartPlayback()
        {
            if (ResourceType == ResourceType.Video)
                VideoViewer.Play();
        }

        public void StopPlayback()
        {
            if (ResourceType == ResourceType.Video)
                VideoViewer.Stop();
        }

        private void StartAll_Click(object sender, RoutedEventArgs e)
        {
            MediaPreviewCoordinator.StartAll();
        }

        private void StopAll_Click(object sender, RoutedEventArgs e)
        {
            MediaPreviewCoordinator.StopAll();
        }

        private void CloseAll_Click(object sender, RoutedEventArgs e)
        {
            MediaPreviewCoordinator.CloseAll();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                MediaPreviewCoordinator.CloseAll();
        }
    }
}