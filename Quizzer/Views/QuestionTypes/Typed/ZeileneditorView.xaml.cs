using Quizzer.DataModels.Questions.Schrittbau;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Quizzer.Views.QuestionTypes.Typed
{
    /// <summary>
    /// Die Maske eines Fragetyps - eine Ansicht für alle fünf, weil sich die Typen nicht in der
    /// <i>Form</i> unterscheiden, sondern darin, welche Teile davon sie zeigen und wie sie sie
    /// nennen.
    /// <para>
    /// Was der Typ bestimmt, sagt sein <c>ZeileneditorViewModel</c>: Überschriften, ob es ein
    /// Häkchen „richtig" gibt, ob sortiert und ob überhaupt eine Zeile angelegt werden darf.
    /// </para>
    /// <para>
    /// Hier dahinter nur Tastatur und Ziehen (E1, M31): Enter geht zur nächsten Zeile, in der
    /// letzten entsteht eine neue; ein Bild auf eine Zeile gezogen oder mit Strg+V eingefügt
    /// hängt es an.
    /// </para>
    /// </summary>
    public partial class ZeileneditorView : UserControl
    {
        public ZeileneditorView()
        {
            InitializeComponent();
        }

        private ZeileneditorViewModel? ViewModel => DataContext as ZeileneditorViewModel;

        private void ZeileEnter(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Return || Keyboard.Modifiers != ModifierKeys.None
                || sender is not FrameworkElement { DataContext: StepZeile zeile } || ViewModel is not { } vm)
                return;

            var index = vm.Zeilen.IndexOf(zeile);

            if (index == vm.Zeilen.Count - 1)
            {
                if (vm.ZeilenAendernVisibility != Visibility.Visible)
                    return;

                vm.AddRowCommand.Execute(null);
            }

            e.Handled = true;
            var ziel = index + 1;

            // Die neue Zeile hat erst nach dem Auslegen ein Textfeld.
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                if (ZeilenListe.ItemContainerGenerator.ContainerFromIndex(ziel) is DependencyObject behaelter
                    && Finde<TextBox>(behaelter, "ZeilenText") is { } feld)
                {
                    feld.Focus();
                    feld.CaretIndex = feld.Text.Length;
                }
            });
        }

        private void MediumDrueber(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void MediumGezogen(object sender, DragEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: StepZeile zeile } && ViewModel is { } vm
                && e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } dateien)
            {
                vm.HaengeDateiAn(zeile, dateien[0]);
                e.Handled = true;
            }
        }

        private void MediumEinfuegen(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.V || Keyboard.Modifiers != ModifierKeys.Control
                || sender is not FrameworkElement { DataContext: StepZeile zeile } || ViewModel is not { } vm)
                return;

            // Text fügt das Textfeld selbst ein - eingegriffen wird nur bei einem Bild oder einer Datei.
            if (QuestionTypes.Zwischenablage.Bild() is { } png)
            {
                var ordner = Path.Combine(Path.GetTempPath(), "quizzer-einfuegen-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(ordner);
                var datei = Path.Combine(ordner, $"eingefuegt-{DateTime.Now:yyyyMMdd-HHmmss}.png");

                try
                {
                    File.WriteAllBytes(datei, png);
                    vm.HaengeDateiAn(zeile, datei);
                }
                finally
                {
                    try { Directory.Delete(ordner, recursive: true); } catch (IOException) { }
                }

                e.Handled = true;
            }
            else if (Clipboard.ContainsFileDropList() && Clipboard.GetFileDropList() is { Count: > 0 } liste && liste[0] is { } pfad)
            {
                vm.HaengeDateiAn(zeile, pfad);
                e.Handled = true;
            }
        }

        private static T? Finde<T>(DependencyObject wurzel, string name) where T : FrameworkElement
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(wurzel); i++)
            {
                var kind = VisualTreeHelper.GetChild(wurzel, i);

                if (kind is T treffer && treffer.Name == name)
                    return treffer;

                if (Finde<T>(kind, name) is { } tiefer)
                    return tiefer;
            }

            return null;
        }
    }
}
