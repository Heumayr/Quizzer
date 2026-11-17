using Quizzer.Base;
using System.Windows;

namespace Quizzer.Views.GameViews.Sub
{
    /// <summary>
    /// Was Beamer und Spielleiter zur Sonderregel einer Zelle sehen (M35 C3): Ueberschrift, Regeltext und
    /// wie viele in der Vorphase abgegeben haben - nie, was. Ein Objekt fuer beide Bildschirme; der
    /// Schrittkontext zeigt ueber seinen Owner hierher, wie bei <see cref="ReaktionAnzeige"/>.
    /// </summary>
    public sealed class SonderregelAnzeige : UcViewModelBase
    {
        /// <summary>Die Ueberschrift des Bereichs beim Spielleiter - „Sonderregel" oder „Joker" (M35 C7b).</summary>
        public string Bereich { get; private set; } = SonderregelBereich;

        internal const string SonderregelBereich = "Sonderregel";

        /// <summary>„Sonderregel: Einsatz".</summary>
        public string Titel { get; private set; } = string.Empty;

        public string Regeltext { get; private set; } = string.Empty;

        /// <summary>„2 von 3 haben abgegeben" - waehrend und nach der Vorphase.</summary>
        public string Stand { get; private set; } = string.Empty;

        /// <summary>Die Zelle traegt eine Regel, die gespielt wird.</summary>
        public bool Aktiv { get; private set; }

        public bool VorphaseLaeuft { get; private set; }

        /// <summary>Was waehrend der Frage dazu gilt, etwa „Kopfgeld auf Anna" (M35 C3b) - mit Symbol, nie allein Farbe.</summary>
        public string Hinweis { get; private set; } = string.Empty;

        /// <summary>Am Beamer ueber der Frage, sobald die Vorphase zu ist.</summary>
        public Visibility HinweisVisibility => Aktiv && !VorphaseLaeuft && Hinweis.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Am Beamer nur waehrend der Vorphase - danach gehoert der Bildschirm der Frage.</summary>
        public Visibility BeamerVisibility => VorphaseLaeuft ? Visibility.Visible : Visibility.Collapsed;

        public Visibility SpielleiterVisibility => Aktiv ? Visibility.Visible : Visibility.Collapsed;

        public Visibility SchliessenVisibility => VorphaseLaeuft ? Visibility.Visible : Visibility.Collapsed;

        internal void Zeige(string titel, string regeltext, string stand, bool vorphaseLaeuft, string hinweis = "", string bereich = SonderregelBereich)
        {
            Bereich = bereich;
            Hinweis = hinweis.Length > 0 ? $"{Quizzer.DataModels.Questions.Sonderregeln.SpecialRuleProfiles.Symbol} {hinweis}" : string.Empty;
            Titel = titel;
            Regeltext = regeltext;
            Stand = stand;
            Aktiv = true;
            VorphaseLaeuft = vorphaseLaeuft;
            Melde();
        }

        internal void Leere()
        {
            Titel = Regeltext = Stand = Hinweis = string.Empty;
            Aktiv = VorphaseLaeuft = false;
            Aufloesung = [];
            Melde();
        }

        /// <summary>Die Auflösung des Finales (M37 E4): was schon aufgedeckt ist, je Zeile Stand, Einsatz und Antwort.</summary>
        public IReadOnlyList<string> Aufloesung { get; private set; } = [];

        /// <summary>Am Beamer erst, wenn der Spielleiter aufdeckt - vorher steht dort nichts von Einsätzen und Antworten.</summary>
        public Visibility AufloesungVisibility => Aktiv && Aufloesung.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        internal void ZeigeAufloesung(IReadOnlyList<string> zeilen)
        {
            Aufloesung = zeilen;
            OnPropertyChanged(nameof(Aufloesung));
            OnPropertyChanged(nameof(AufloesungVisibility));
        }

        private void Melde()
        {
            OnPropertyChanged(nameof(Bereich));
            OnPropertyChanged(nameof(Titel));
            OnPropertyChanged(nameof(Regeltext));
            OnPropertyChanged(nameof(Stand));
            OnPropertyChanged(nameof(Aktiv));
            OnPropertyChanged(nameof(VorphaseLaeuft));
            OnPropertyChanged(nameof(BeamerVisibility));
            OnPropertyChanged(nameof(SpielleiterVisibility));
            OnPropertyChanged(nameof(SchliessenVisibility));
            OnPropertyChanged(nameof(Hinweis));
            OnPropertyChanged(nameof(HinweisVisibility));
            OnPropertyChanged(nameof(Aufloesung));
            OnPropertyChanged(nameof(AufloesungVisibility));
        }
    }
}
