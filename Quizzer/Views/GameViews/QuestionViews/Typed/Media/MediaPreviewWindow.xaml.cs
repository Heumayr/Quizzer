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
        public override bool UsePlacementPersistence => false;

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
                        var image = Bildlader.Lade(FilePath);

                        if (image == null)
                        {
                            // Ohne diesen Zweig wirft der Fensteraufbau, und der Spielleiter
                            // bekommt statt der Vorschau ein Fehlerfenster.
                            Hinweis.Text = "Dieses Bild lässt sich nicht anzeigen: "
                                         + System.IO.Path.GetFileName(FilePath);
                            Hinweis.Visibility = Visibility.Visible;
                            break;
                        }

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

        /// <summary>
        /// Ein Video, das sich nicht abspielen lässt, sagt es - statt als schwarzes, stummes
        /// Fenster dazustehen.
        /// <para>
        /// <b>Es gab im ganzen Projekt keinen einzigen <c>MediaFailed</c>-Behandler</b>
        /// (2026-09-07 nachgemessen). <c>MediaElement</c> wirft dabei nicht: das Ereignis läuft
        /// die Baumhierarchie hoch, und wenn es niemand nimmt, geschieht <b>nichts</b>. Genau so
        /// verhält sich ein <c>.webm</c> ohne die Windows-Erweiterung.
        /// </para>
        /// <para>
        /// Derselbe Weg wie beim unlesbaren Bild eine Zeile darüber - dieselbe Textzeile,
        /// dieselbe Stelle.
        /// </para>
        /// </summary>
        private void VideoViewer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            VideoViewer.Visibility = Visibility.Collapsed;

            Hinweis.Text = "Dieses Medium lässt sich nicht abspielen: "
                + System.IO.Path.GetFileName(FilePath);

            Hinweis.Visibility = Visibility.Visible;
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