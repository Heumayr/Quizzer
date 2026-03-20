using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Quizzer.Views.GameViews.QuestionViews.Typed
{
    public partial class ResourceViewerControl : UserControlStepViewBase
    {
        public ResourceViewerControl()
        {
            InitializeComponent();
            DataContextChanged += ResourceViewerControl_DataContextChanged;
        }

        public string? RootFolder
        {
            get => (string?)GetValue(RootFolderProperty);
            set => SetValue(RootFolderProperty, value);
        }

        public static readonly DependencyProperty RootFolderProperty =
            DependencyProperty.Register(
                nameof(RootFolder),
                typeof(string),
                typeof(ResourceViewerControl),
                new PropertyMetadata(null, OnRootFolderChanged));

        private static void OnRootFolderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ResourceViewerControl ctrl)
            {
                ctrl.RefreshView();
            }
        }

        private void ResourceViewerControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is INotifyPropertyChanged oldNpc)
                oldNpc.PropertyChanged -= Step_PropertyChanged;

            if (e.NewValue is INotifyPropertyChanged newNpc)
                newNpc.PropertyChanged += Step_PropertyChanged;

            //RefreshView();
        }

        private void Step_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(QuestionStepResource.ResourceFileName) ||
                e.PropertyName == nameof(QuestionStepResource.ResourceTyp))
            {
                RefreshView();
            }
        }

        public string? AudioPlaceholderFile
        {
            get => (string?)GetValue(AudioPlaceholderFileProperty);
            set => SetValue(AudioPlaceholderFileProperty, value);
        }

        public static readonly DependencyProperty AudioPlaceholderFileProperty =
            DependencyProperty.Register(
                nameof(AudioPlaceholderFile),
                typeof(string),
                typeof(ResourceViewerControl),
                new PropertyMetadata(null, OnAudioPlaceholderFileChanged));

        private static void OnAudioPlaceholderFileChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ResourceViewerControl ctrl)
                ctrl.RefreshView();
        }

        public override void RefreshView()
        {
            ResetView();
            SetMasterVisibility();

            if (DataContext is not QuestionStepResource step)
                return;

            if (string.IsNullOrWhiteSpace(step.ResourceFileName))
                return;

            if (string.IsNullOrWhiteSpace(RootFolder))
                return;

            var fullPath = Path.Combine(RootFolder, step.ResourceFileName);

            if (!File.Exists(fullPath))
                return;

            switch (step.ResourceTyp)
            {
                case ResourceType.Image:
                    ShowImage(fullPath);
                    break;

                case ResourceType.Audio:
                    ShowAudio(fullPath);
                    break;

                case ResourceType.Video:
                    ShowVideo(fullPath);
                    break;

                case ResourceType.Document:
                    ShowDocument();
                    break;
            }
        }

        private void ResetView()
        {
            ImgPreview.Source = null;
            ImgPreview.Visibility = Visibility.Collapsed;
            ImageContainer.Visibility = Visibility.Collapsed;

            AudioPlaceholder.Source = null;
            AudioPlaceholder.Visibility = Visibility.Collapsed;

            MediaPlayer.Stop();
            MediaPlayer.Source = null;
            MediaPlayer.Visibility = Visibility.Collapsed;

            MediaPlayerBorder.Visibility = Visibility.Collapsed;
            MediaContainer.Visibility = Visibility.Collapsed;

            DocumentContainer.Visibility = Visibility.Collapsed;
        }

        private void SetMasterVisibility()
        {
            MediaDesignation.Visibility = IsMasterView
                ? Visibility.Visible
                : Visibility.Collapsed;

            MediaControls.Visibility = IsMasterView
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void ShowImage(string fullPath)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(fullPath, UriKind.Absolute);
            image.EndInit();
            image.Freeze();

            ImgPreview.Source = image;
            ImgPreview.Visibility = Visibility.Visible;
            ImageContainer.Visibility = Visibility.Visible;
        }

        private void ShowAudio(string fullPath)
        {
            MediaPlayer.Source = new Uri(fullPath, UriKind.Absolute);

            if (!string.IsNullOrWhiteSpace(AudioPlaceholderFile) && File.Exists(AudioPlaceholderFile))
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(AudioPlaceholderFile, UriKind.Absolute);
                image.EndInit();
                image.Freeze();

                AudioPlaceholder.Source = image;
                AudioPlaceholder.Visibility = Visibility.Visible;
            }

            MediaPlayerBorder.Visibility = Visibility.Visible;
            MediaContainer.Visibility = Visibility.Visible;
        }

        private void ShowVideo(string fullPath)
        {
            MediaPlayer.Source = new Uri(fullPath, UriKind.Absolute);
            MediaPlayerBorder.Visibility = Visibility.Visible;
            MediaContainer.Visibility = Visibility.Visible;
        }

        private void ShowDocument()
        {
            DocumentContainer.Visibility = Visibility.Visible;
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            MediaPlayer.Play();
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            MediaPlayer.Stop();
        }
    }
}