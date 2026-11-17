using System.Windows;

namespace Quizzer.Views.GameViews.Sub
{
    /// <summary>
    /// Die Tafeln der Siegerehrung in ihrer Reihenfolge (M37 E3) - der Sieger kommt immer zuerst. Im Teamspiel ist der
    /// Sieger das Team, danach kommt die Einzelwertung (T3); Rückblick und Verlauf bleiben je Person.
    /// </summary>
    public enum SiegerehrungTafel
    {
        Sieger,
        Einzelwertung,
        Rueckblick,
        Verlauf,
    }

    /// <summary>
    /// Welche Tafel die Siegerehrung zeigt (M37 E3). Beamer und Spielleiterleiste teilen diesen Stand; geblättert wird
    /// über den Spielleiter (<c>GameMasterViewModel.Siegerehrung</c>), eine Tafel ohne Inhalt wird übersprungen.
    /// </summary>
    public partial class StatsContext
    {
        private SiegerehrungTafel tafel = SiegerehrungTafel.Sieger;

        public SiegerehrungTafel Tafel => tafel;

        /// <summary>Der Rückblick auf den Abend (M37 E04); <c>null</c>, solange keiner geladen ist oder er leer war.</summary>
        public AbendrueckblickViewModel? Rueckblick { get; private set; }

        /// <summary>Der Punkteverlauf (M37 E06); <c>null</c>, solange keiner geladen ist oder einer gespielten Zelle der Zeitpunkt fehlt.</summary>
        public PunkteverlaufViewModel? Verlauf { get; private set; }

        /// <summary>Die Spielerkarten der Siegerehrung - ohne Teams als Sieger, im Teamspiel als Einzelwertung danach.</summary>
        public Visibility SiegerVisibility => Zeigt(IstTeamspiel ? SiegerehrungTafel.Einzelwertung : SiegerehrungTafel.Sieger);

        /// <summary>Die Siegerteams (M37 T3) - im Teamspiel die erste Tafel.</summary>
        public Visibility TeamSiegerVisibility => IstTeamspiel ? Zeigt(SiegerehrungTafel.Sieger) : Visibility.Collapsed;

        public Visibility RueckblickVisibility => Zeigt(SiegerehrungTafel.Rueckblick);

        public Visibility VerlaufVisibility => Zeigt(SiegerehrungTafel.Verlauf);

        /// <summary>Die Tafeln mit Inhalt, in ihrer Reihenfolge.</summary>
        public IReadOnlyList<SiegerehrungTafel> Tafeln
        {
            get
            {
                var tafeln = new List<SiegerehrungTafel> { SiegerehrungTafel.Sieger };

                if (IstTeamspiel)
                    tafeln.Add(SiegerehrungTafel.Einzelwertung);

                if (Rueckblick is { IstLeer: false })
                    tafeln.Add(SiegerehrungTafel.Rueckblick);

                if (Verlauf != null)
                    tafeln.Add(SiegerehrungTafel.Verlauf);

                return tafeln;
            }
        }

        /// <summary>Die Tafel nach der laufenden; nach der letzten wieder der Sieger.</summary>
        public SiegerehrungTafel NaechsteTafel
        {
            get
            {
                var tafeln = Tafeln.ToList();
                return tafeln[(tafeln.IndexOf(tafel) + 1) % tafeln.Count];
            }
        }

        /// <summary>
        /// Was der Knopf des Spielleiters als Nächstes tut. Vom Sieger aus lädt er erst - deshalb dort „Rückblick", im Teamspiel
        /// die Einzelwertung, die es immer gibt.
        /// </summary>
        public string NaechsteTafelText => tafel == SiegerehrungTafel.Sieger && !IstTeamspiel
            ? "Rückblick zeigen"
            : NaechsteTafel switch
            {
                SiegerehrungTafel.Einzelwertung => "Einzelwertung zeigen",
                SiegerehrungTafel.Rueckblick => "Rückblick zeigen",
                SiegerehrungTafel.Verlauf => "Punkteverlauf zeigen",
                _ => "Sieger zeigen",
            };

        internal void SetzeRueckblick(AbendrueckblickViewModel? rueckblick)
        {
            Rueckblick = rueckblick;
            OnPropertyChanged(nameof(Rueckblick));
            OnPropertyChanged(nameof(NaechsteTafelText));
        }

        internal void SetzeVerlauf(PunkteverlaufViewModel? verlauf)
        {
            Verlauf = verlauf;
            OnPropertyChanged(nameof(Verlauf));
            OnPropertyChanged(nameof(NaechsteTafelText));
        }

        internal void ZeigeTafel(SiegerehrungTafel neu)
        {
            tafel = neu;
            OnPropertyChanged(nameof(Tafel));
            TafelnGeaendert();
        }

        /// <summary>Die Sichtbarkeiten hängen auch am Spielende - <see cref="UpdateScores"/> ruft es nach jedem Zug.</summary>
        private void TafelnGeaendert()
        {
            OnPropertyChanged(nameof(SiegerVisibility));
            OnPropertyChanged(nameof(TeamSiegerVisibility));
            OnPropertyChanged(nameof(RueckblickVisibility));
            OnPropertyChanged(nameof(VerlaufVisibility));
            OnPropertyChanged(nameof(NaechsteTafelText));
        }

        /// <summary>Ein wieder offenes Spiel (Rücknahme der letzten Frage) beginnt die nächste Siegerehrung beim Sieger.</summary>
        private void ZurueckZumSieger()
        {
            if (tafel != SiegerehrungTafel.Sieger)
                ZeigeTafel(SiegerehrungTafel.Sieger);
        }

        /// <summary>Nur in der Siegerehrung - davor steht die Punkteleiste.</summary>
        private Visibility Zeigt(SiegerehrungTafel welche) => IsGameFinished && tafel == welche ? Visibility.Visible : Visibility.Collapsed;
    }
}
