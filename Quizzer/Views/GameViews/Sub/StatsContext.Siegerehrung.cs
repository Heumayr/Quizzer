using System.Windows;

namespace Quizzer.Views.GameViews.Sub
{
    /// <summary>Die Tafeln der Siegerehrung in ihrer Reihenfolge (M37 E3) - der Sieger kommt immer zuerst.</summary>
    public enum SiegerehrungTafel
    {
        Sieger,
        Rueckblick,
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

        public Visibility SiegerVisibility => Zeigt(SiegerehrungTafel.Sieger);

        public Visibility RueckblickVisibility => Zeigt(SiegerehrungTafel.Rueckblick);

        /// <summary>Die Tafeln mit Inhalt, in ihrer Reihenfolge.</summary>
        public IReadOnlyList<SiegerehrungTafel> Tafeln
        {
            get
            {
                var tafeln = new List<SiegerehrungTafel> { SiegerehrungTafel.Sieger };

                if (Rueckblick is { IstLeer: false })
                    tafeln.Add(SiegerehrungTafel.Rueckblick);

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

        /// <summary>Was der Knopf des Spielleiters als Nächstes tut. Vom Sieger aus lädt er erst - deshalb dort immer „Rückblick".</summary>
        public string NaechsteTafelText => tafel == SiegerehrungTafel.Sieger
            ? "Rückblick zeigen"
            : NaechsteTafel switch
            {
                SiegerehrungTafel.Rueckblick => "Rückblick zeigen",
                _ => "Sieger zeigen",
            };

        internal void SetzeRueckblick(AbendrueckblickViewModel? rueckblick)
        {
            Rueckblick = rueckblick;
            OnPropertyChanged(nameof(Rueckblick));
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
            OnPropertyChanged(nameof(RueckblickVisibility));
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
