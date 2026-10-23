using Quizzer.Base;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Quizzer.Views.QuestionTypes
{
    /// <summary>Der Dialog „Entwurf mit KI" - Ziehen und Einfügen von Bildern hier, der Rest im ViewModel.</summary>
    public partial class KiEntwurfView : WindowBase
    {
        public KiEntwurfView()
        {
            InitializeComponent();
            PreviewKeyDown += Einfuegen;
            Loaded += (_, _) => ThemaFeld.Focus();
        }

        public KiEntwurfViewModel? ViewModel => DataContext as KiEntwurfViewModel;

        private void BildDrueber(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void BildGezogen(object sender, DragEventArgs e)
        {
            if (ViewModel is not { } vm || e.Data.GetData(DataFormats.FileDrop) is not string[] dateien)
                return;

            foreach (var datei in dateien)
                vm.NimmBild(datei);
        }

        /// <summary>
        /// Strg+V mit einem Bild in der Zwischenablage (etwa ein Bildschirmausschnitt) - Text
        /// fügt das Textfeld weiter selbst ein.
        /// </summary>
        private void Einfuegen(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.V || Keyboard.Modifiers != ModifierKeys.Control || ViewModel is not { } vm)
                return;

            if (Zwischenablage.Bild() is { } png)
            {
                vm.NimmBild($"eingefügt-{DateTime.Now:HHmmss}.png", "image/png", png);
                e.Handled = true;
            }
            else if (Clipboard.ContainsFileDropList())
            {
                foreach (var datei in Clipboard.GetFileDropList())
                    if (datei != null)
                        vm.NimmBild(datei);

                e.Handled = true;
            }
        }
    }

    /// <summary>Das Bild der Zwischenablage als PNG - für den KI-Dialog und die Zeilen des Editors.</summary>
    internal static class Zwischenablage
    {
        public static byte[]? Bild()
        {
            try
            {
                if (!Clipboard.ContainsImage() || Clipboard.GetImage() is not { } bild)
                    return null;

                var png = new PngBitmapEncoder();
                png.Frames.Add(BitmapFrame.Create(bild));

                using var strom = new MemoryStream();
                png.Save(strom);
                return strom.ToArray();
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                // Die Zwischenablage ist gerade von einem anderen Programm belegt.
                return null;
            }
        }
    }
}
