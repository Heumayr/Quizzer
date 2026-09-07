using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Views.GameViews.QuestionViews.Typed.Media;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Quizzer.Views.GameViews.QuestionViews.Typed
{
    public partial class ResourceViewerControl : UserControlStepViewBase
    {
        private string? _currentFullPath;
        private string? _lastRefreshKey;

        private static readonly Dictionary<string, BitmapImage> _imageCache = new();

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

        private static void OnRootFolderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ResourceViewerControl ctrl)
                ctrl.RefreshView();
        }

        private static void OnAudioPlaceholderFileChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ResourceViewerControl ctrl)
                ctrl.RefreshView();
        }

        private void ResourceViewerControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is INotifyPropertyChanged oldNpc)
                oldNpc.PropertyChanged -= Step_PropertyChanged;

            if (e.NewValue is INotifyPropertyChanged newNpc)
                newNpc.PropertyChanged += Step_PropertyChanged;

            RefreshView();
        }

        private void Step_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(QuestionStepResource.ResourceFileName) ||
                e.PropertyName == nameof(QuestionStepResource.ResourceTyp))
            {
                RefreshView();
            }
        }

        public override void RefreshView()
        {
            SetMasterVisibility();

            if (DataContext is not QuestionStepResource step)
            {
                ResetView();
                _currentFullPath = null;
                _lastRefreshKey = null;
                return;
            }

            var refreshKey = $"{RootFolder}|{step.ResourceTyp}|{step.ResourceFileName}|{AudioPlaceholderFile}|{IsMasterView}";
            if (_lastRefreshKey == refreshKey)
                return;

            _lastRefreshKey = refreshKey;

            ResetView();
            _currentFullPath = null;

            if (string.IsNullOrWhiteSpace(step.ResourceFileName))
                return;

            if (string.IsNullOrWhiteSpace(RootFolder))
                return;

            var fullPath = Path.Combine(RootFolder, step.ResourceFileName);
            if (!File.Exists(fullPath))
                return;

            _currentFullPath = fullPath;

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

            MediaHinweis.Text = string.Empty;
            MediaHinweis.Visibility = Visibility.Collapsed;

            try
            {
                MediaPlayer.Stop();
            }
            catch
            {
            }

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
            if (!_imageCache.TryGetValue(fullPath, out var image))
            {
                var geladen = Bildlader.Lade(fullPath);

                if (geladen == null)
                {
                    // Nicht in den Zwischenspeicher: ein spaeter ersetztes Bild soll beim
                    // naechsten Schritt wieder versucht werden.
                    ZeigeHinweis($"Dieses Bild lässt sich nicht anzeigen: {Path.GetFileName(fullPath)}");

                    return;
                }

                image = geladen;
                _imageCache[fullPath] = image;
            }

            ImgPreview.Source = image;
            ImgPreview.Visibility = Visibility.Visible;
            ImageContainer.Visibility = Visibility.Visible;
        }

        /// <summary>Zeigt statt des Mediums einen lesbaren Satz.</summary>
        private void ZeigeHinweis(string text)
        {
            MediaHinweis.Text = text;
            MediaHinweis.Visibility = Visibility.Visible;
        }

        private void ShowAudio(string fullPath)
        {
            MediaPlayer.Source = new Uri(fullPath, UriKind.Absolute);

            if (!string.IsNullOrWhiteSpace(AudioPlaceholderFile) && File.Exists(AudioPlaceholderFile))
            {
                if (!_imageCache.TryGetValue(AudioPlaceholderFile, out var image))
                {
                    var geladen = Bildlader.Lade(AudioPlaceholderFile);

                    if (geladen != null)
                        _imageCache[AudioPlaceholderFile] = geladen;

                    image = geladen!;
                }

                // Anders als beim eigentlichen Bild kein Hinweis: der Platzhalter ist Schmuck,
                // der Ton laeuft ohne ihn genauso. Ein Satz darueber waere nur Laerm.
                if (image != null)
                {
                    AudioPlaceholder.Source = image;
                    AudioPlaceholder.Visibility = Visibility.Visible;
                }
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

        /// <summary>
        /// Ein Medium, das sich nicht abspielen lässt, sagt es - statt als schwarze, stumme
        /// Fläche dazustehen.
        /// <para>
        /// <b>Es gab im ganzen Projekt keinen einzigen <c>MediaFailed</c>-Behandler</b>
        /// (2026-09-07 nachgemessen). <c>MediaElement</c> wirft dabei nicht: das Ereignis läuft
        /// die Baumhierarchie hoch, und wenn es niemand nimmt, geschieht <b>nichts</b>. Der
        /// Spielleiter drückt „Abspielen", es passiert nichts, und niemand erfährt warum. Genau
        /// so verhält sich ein <c>.webm</c> ohne die Windows-Erweiterung.
        /// </para>
        /// <para>
        /// Derselbe Weg wie beim unlesbaren Bild: die Fläche verschwindet, der Satz erscheint.
        /// Ein Fehlerfenster mit Stapelspur wäre mitten im Abend das Gegenteil von Hilfe.
        /// </para>
        /// </summary>
        private void MediaPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            var name = MediaPlayer.Source == null
                ? "Die Datei"
                : Path.GetFileName(MediaPlayer.Source.LocalPath);

            MediaPlayerBorder.Visibility = Visibility.Collapsed;
            MediaControls.Visibility = Visibility.Collapsed;
            MediaContainer.Visibility = Visibility.Visible;

            ZeigeHinweis($"Dieses Medium lässt sich nicht abspielen: {name}");
        }

        private void ShowDocument()
        {
            DocumentContainer.Visibility = Visibility.Visible;
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            MediaPreviewCoordinator.RegisterStartedMedia(this);
            MediaPlayer.Play();
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            MediaPlayer.Stop();
            MediaPreviewCoordinator.UnsignStartedMedia(this);
        }

        private void Preview_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not QuestionStepResource step)
                return;

            if (step.ResourceTyp != ResourceType.Image && step.ResourceTyp != ResourceType.Video)
                return;

            if (string.IsNullOrWhiteSpace(_currentFullPath) || !File.Exists(_currentFullPath))
                return;

            MediaPreviewCoordinator.ShowOnAll(_currentFullPath, step.ResourceTyp);
        }
    }
}