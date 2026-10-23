using Quizzer.Base;
using System.Runtime.InteropServices;
using System.Windows;

namespace Quizzer.Views.HelperViewModels
{
    /// <summary>
    /// Das Meldungsfenster der App (M29): Hinweise, Ja/Nein-Fragen und Fehler im selben Design wie
    /// alle anderen Fenster - vorher das weiße Windows-Meldungsfenster bzw. ein ungestaltetes
    /// Fehlerfenster.
    /// </summary>
    public partial class PromptView : WindowBase
    {
        public PromptView()
        {
            InitializeComponent();
            Loaded += (_, _) => Hauptknopf.Focus();
        }

        public PromptView(PromptViewModel inhalt) : this() => DataContext = inhalt;

        private PromptViewModel? Inhalt => DataContext as PromptViewModel;

        /// <summary>Modal über dem aktiven Fenster; liefert, ob der Hauptknopf gewählt wurde.</summary>
        public static bool ShowModal(PromptViewModel inhalt)
        {
            var fenster = new PromptView(inhalt);
            PlaceOverActiveWindow(fenster);
            fenster.ShowDialog();
            return inhalt.Result;
        }

        /// <summary>Ein Fehler, ohne den Ablauf anzuhalten - er kann aus jedem Rückruf kommen.</summary>
        public static void ShowError(Exception ex)
        {
            var fenster = new PromptView(PromptViewModel.ForException(ex)) { ShowInTaskbar = true };
            PlaceOverActiveWindow(fenster);
            fenster.Show();
        }

        private static void PlaceOverActiveWindow(Window fenster)
        {
            var aktiv = Application.Current?.Windows.OfType<Window>()
                .FirstOrDefault(w => w != fenster && w.IsActive && w.IsVisible);

            if (aktiv != null)
                fenster.Owner = aktiv;
            else
                fenster.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        private void Haupt_Click(object sender, RoutedEventArgs e)
        {
            if (Inhalt != null)
                Inhalt.Result = true;
            Close();
        }

        private void Nein_Click(object sender, RoutedEventArgs e)
        {
            if (Inhalt != null)
                Inhalt.Result = false;
            Close();
        }

        private void Einzelheiten_Click(object sender, RoutedEventArgs e)
        {
            var zeigen = Einzelheiten.Visibility != Visibility.Visible;
            Einzelheiten.Visibility = zeigen ? Visibility.Visible : Visibility.Collapsed;
            EinzelheitenKnopf.Content = zeigen ? "Einzelheiten ausblenden" : "Einzelheiten zeigen";
        }

        private void Kopieren_Click(object sender, RoutedEventArgs e)
        {
            if (Inhalt == null || sender is not System.Windows.Controls.Button knopf)
                return;

            try
            {
                Clipboard.SetText(Inhalt.Message + Environment.NewLine + Environment.NewLine + Inhalt.Details);
                knopf.Content = "Kopiert";
            }
            catch (ExternalException)
            {
                // Die Zwischenablage ist kurz von einem anderen Programm belegt - das sagt der Knopf.
                knopf.Content = "Zwischenablage belegt - noch einmal";
            }
        }
    }
}
