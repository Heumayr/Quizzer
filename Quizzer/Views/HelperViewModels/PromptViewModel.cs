using System.Windows;
using System.Windows.Media;

namespace Quizzer.Views.HelperViewModels
{
    /// <summary>Welche Art Meldung - bestimmt Wort, Zeichen, Farbe und Knöpfe.</summary>
    public enum PromptKind
    {
        Info,
        Question,
        Error,
    }

    /// <summary>
    /// Inhalt des Meldungsfensters (M29): ein Hinweis, eine Ja/Nein-Frage oder ein Fehler.
    /// <para>
    /// <b>Nie nur über Farbe</b> (standards-bedienbarkeit, Regel 3): jede Art trägt ein Wort und ein
    /// Zeichen; die Farbe kommt nur dazu.
    /// </para>
    /// </summary>
    public sealed class PromptViewModel
    {
        /// <summary>Für die Prüflisten, die jedes Fenster ohne Angaben aufbauen.</summary>
        public PromptViewModel() : this(PromptKind.Info, string.Empty, string.Empty) { }

        public PromptViewModel(PromptKind kind, string message, string caption, string? details = null)
        {
            Kind = kind;
            Message = message;
            Caption = string.IsNullOrWhiteSpace(caption) ? "Quizzer" : caption;
            Details = details ?? string.Empty;
        }

        /// <summary>Ein Fehler aus einer Ausnahme: die Meldungen der ganzen Kette oben, das Technische aufklappbar.</summary>
        public static PromptViewModel ForException(Exception ex)
        {
            var meldungen = new List<string>();
            for (var aktuell = ex; aktuell != null; aktuell = aktuell.InnerException)
            {
                if (!meldungen.Contains(aktuell.Message))
                    meldungen.Add(aktuell.Message);
            }

            return new PromptViewModel(PromptKind.Error, string.Join(Environment.NewLine, meldungen),
                "Ein Fehler ist aufgetreten", ex.ToString());
        }

        public PromptKind Kind { get; }

        public string Caption { get; }

        public string Message { get; }

        /// <summary>Technische Einzelheiten - nur bei Fehlern, zum Weitergeben.</summary>
        public string Details { get; }

        public string Heading => Kind switch
        {
            PromptKind.Question => "Frage",
            PromptKind.Error => "Fehler",
            _ => "Hinweis",
        };

        public string Symbol => Kind switch
        {
            PromptKind.Question => "?",
            PromptKind.Error => "!",
            _ => "i",
        };

        public Brush AccentBrush => Kind switch
        {
            PromptKind.Question => new SolidColorBrush(Color.FromRgb(0xE8, 0xB5, 0x63)),
            PromptKind.Error => new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x80)),
            _ => new SolidColorBrush(Color.FromRgb(0x79, 0xB8, 0xFF)),
        };

        public string PrimaryText => Kind switch
        {
            PromptKind.Question => "Ja",
            PromptKind.Error => "Schließen",
            _ => "OK",
        };

        public string PrimaryToolTip => Kind switch
        {
            PromptKind.Question => "Bestätigt die Frage. Mit der Eingabetaste dasselbe.",
            PromptKind.Error => "Schließt die Meldung. Das Programm läuft weiter.",
            _ => "Schließt den Hinweis. Mit der Eingabetaste dasselbe.",
        };

        public Visibility SecondaryVisibility => Kind == PromptKind.Question ? Visibility.Visible : Visibility.Collapsed;

        public Visibility DetailsVisibility => string.IsNullOrWhiteSpace(Details) ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>Was gewählt wurde: <c>true</c> nur nach dem Hauptknopf. Schließen und Escape heißen „Nein".</summary>
        public bool Result { get; set; }
    }
}
