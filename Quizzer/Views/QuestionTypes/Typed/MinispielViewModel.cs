using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Schrittbau;
using System.Windows;

namespace Quizzer.Views.QuestionTypes.Typed
{
    /// <summary>
    /// Minispiel (M35 B2): Art und Parameter ueber den Zeilen, darunter die Optionen.
    /// <para>
    /// <b>Art und Parameter schreiben sofort in die Frage</b> - sie sind Spalten, keine Schritte;
    /// der Uebersetzer raeumt daran nichts weg. Was die Art liest, sagt ihr Profil.
    /// </para>
    /// </summary>
    public sealed class MinispielViewModel : ZeileneditorViewModel
    {
        private readonly MinigameQuestion? minispiel;

        internal MinispielViewModel(IStepComposer composer, QuestionBase frage, Action melde)
            : base(composer, frage, melde)
        {
            minispiel = frage as MinigameQuestion;
        }

        public override object? Zusatzmaske => minispiel == null ? null : this;

        /// <summary>Die Arten zur Auswahl.</summary>
        public IReadOnlyList<MinigameProfile> Arten => MinigameProfiles.All;

        /// <summary>Die gewaehlte Art. Ein Wechsel setzt die Parameter auf deren Vorgaben.</summary>
        public MinigameProfile? Art
        {
            get => minispiel != null && MinigameProfiles.TryFor(minispiel.Kind, out var art) ? art : null;
            set
            {
                if (minispiel == null || value == null || value.Kind == minispiel.Kind)
                    return;

                value.ApplyDefaults(minispiel);
                Geaendert();
            }
        }

        /// <summary>Die Spielweisen der Art, als Namen - dieselbe Liste je Art (siehe <c>VariantNames</c>).</summary>
        public IReadOnlyList<string> Varianten => Art?.VariantNames ?? KeineVarianten;

        private static readonly IReadOnlyList<string> KeineVarianten = [];

        public int Variante
        {
            get => minispiel?.Variant ?? 0;
            set
            {
                // -1 heisst bei der Auswahlliste "nichts gewaehlt", nicht "Spielweise -1".
                if (value >= 0)
                    Setze(value, m => m.Variant, (m, v) => m.Variant = v);
            }
        }

        public int Dauer
        {
            get => minispiel?.DurationSeconds ?? 0;
            set => Setze(value, m => m.DurationSeconds, (m, v) => m.DurationSeconds = v);
        }

        public int Runden
        {
            get => minispiel?.Rounds ?? 0;
            set => Setze(value, m => m.Rounds, (m, v) => m.Rounds = v);
        }

        public int Anteil
        {
            get => minispiel?.Share ?? 0;
            set => Setze(value, m => m.Share, (m, v) => m.Share = v);
        }

        public Visibility VarianteVisibility => Sichtbar(Varianten.Count > 1);

        public Visibility DauerVisibility => Sichtbar(Art?.ShowDurationSeconds == true);

        /// <summary>„Dauer (Sekunden)" oder, bei der Stoppuhr, „Zielzeit (Sekunden)" - aus dem Profil.</summary>
        public string DauerBeschriftung => Art?.DurationLabel ?? string.Empty;

        public string DauerErklaerung => Art?.DurationToolTip ?? string.Empty;

        public Visibility RundenVisibility => Sichtbar(Art?.ShowRounds == true);

        public Visibility AnteilVisibility => Sichtbar(Art?.ShowShare == true);

        /// <summary>Wie gespielt und gewertet wird - fuer den Spielleiter.</summary>
        public string Beschreibung => Art?.Description ?? string.Empty;

        /// <summary>Was am Beamer ueber den Optionen steht.</summary>
        public string Regelzeile
            => minispiel == null ? string.Empty : $"Am Beamer steht: „{MinigameProfiles.RulesFor(minispiel)}“";

        public override bool DarfSortieren => true;

        /// <summary>Die Art benennt ihre Zeilen („Aussagen", M35 C5); ohne Mindestzahl gilt die Ueberschrift des Typs.</summary>
        public override string ZeilenTitel
            => Art is { MinOptions: > 0 } art
                ? art.OptionsName + (art.ShowTruePerRow ? " (Haken = wahr)" : string.Empty)
                : base.ZeilenTitel;

        /// <summary>Der Haken je Zeile nur, wo die Art ihn liest - bei der Blitzrunde heisst er „wahr".</summary>
        public override bool ZeigtRichtig => Art?.ShowTruePerRow == true;

        public override string RichtigBeschriftung => "wahr";

        public override string Tastenerklaerung
            => "Diese Taste liegt im Spiel auf dieser Option - am Telefon und am Beamer.";

        /// <summary>
        /// Eine Art ohne Zeilen (Reaktion, M35 B3) zeigt die Liste nur, solange darin noch Inhalt aus einer
        /// anderen Art steht - sonst liessen sich die Reste nicht mehr entfernen.
        /// </summary>
        public override Visibility ZeilenVisibility
            => Art?.MinOptions == 0 && Zeilen.All(z => z.IstLeer) ? Visibility.Collapsed : base.ZeilenVisibility;

        public override string Zeilenhinweis
        {
            get
            {
                var optionen = Zeilen.Count(z => !z.IstLeer);
                var mindestens = Art?.MinOptions ?? 0;

                if (Art is { MinOptions: 0 } ohneZeilen)
                    return $"{ohneZeilen.DisplayName} braucht keine Zeilen – gespielt wird am Telefon."
                           + (optionen > 0 ? " Was hier noch steht, wird nicht gespielt." : string.Empty);

                if (Art?.Kind == MinigameKind.LightningRound)
                    return $"{optionen} Aussagen, davon {Zeilen.Count(z => !z.IstLeer && z.IstRichtig)} wahr (mindestens "
                           + $"{mindestens}). Am Telefon kommen sie einzeln und gemischt; richtig +1, falsch −1.";

                if (Art?.Kind == MinigameKind.WordScramble)
                    return $"{Zeilen.Count(z => !z.IstLeer && Art.CountsAsOption(z.Text))} Begriffe (mindestens {mindestens}). Der "
                           + "Server verwürfelt die Buchstaben; wer einen zuerst richtig eintippt, löst ihn. Begriffe aus "
                           + "einem Buchstaben oder lauter gleichen werden nicht gespielt.";

                if (Art?.Kind == MinigameKind.ExplainIt)
                    return $"{Zeilen.Count(z => !z.IstLeer && Art.CountsAsOption(z.Text))} Begriffe (mindestens "
                           + $"{Math.Max(mindestens, minispiel?.Rounds ?? 0)} – je Runde einer). Nur das Telefon des Erklärers "
                           + "zeigt ihn; wer ihn zuerst richtig eintippt, gewinnt die Runde.";

                if (Art?.Kind == MinigameKind.Bluff)
                    return $"{Zeilen.Count(z => !z.IstLeer && Art.CountsAsOption(z.Text))} Antworten (mindestens {mindestens}). Die "
                           + "erste ist die echte, weitere gelten als gleichwertige Schreibweisen – ein Bluff, der einer davon gleicht, "
                           + "ist von selbst gestrichen.";

                return $"{optionen} Optionen (mindestens {mindestens}). Jeder wählt am Telefon geheim "
                       + "genau eine. Die Punkte sind ein Vorschlag auf der Spielerkarte; Minuspunkte "
                       + "gibt es keine.";
            }
        }

        protected override void Melde()
        {
            base.Melde();

            OnPropertyChanged(nameof(Art));
            OnPropertyChanged(nameof(Varianten));
            OnPropertyChanged(nameof(Variante));
            OnPropertyChanged(nameof(Dauer));
            OnPropertyChanged(nameof(Runden));
            OnPropertyChanged(nameof(Anteil));
            OnPropertyChanged(nameof(VarianteVisibility));
            OnPropertyChanged(nameof(DauerVisibility));
            OnPropertyChanged(nameof(DauerBeschriftung));
            OnPropertyChanged(nameof(DauerErklaerung));
            OnPropertyChanged(nameof(RundenVisibility));
            OnPropertyChanged(nameof(AnteilVisibility));
            OnPropertyChanged(nameof(Beschreibung));
            OnPropertyChanged(nameof(Regelzeile));
            OnPropertyChanged(nameof(ZeilenVisibility));
            OnPropertyChanged(nameof(ZeilenTitel));
            OnPropertyChanged(nameof(RichtigVisibility));
        }

        private void Setze(int wert, Func<MinigameQuestion, int> lies, Action<MinigameQuestion, int> schreib)
        {
            if (minispiel == null || lies(minispiel) == wert)
                return;

            schreib(minispiel, wert);
            Geaendert();
        }

        private static Visibility Sichtbar(bool ja) => ja ? Visibility.Visible : Visibility.Collapsed;
    }
}
