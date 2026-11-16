using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Sonderregeln;

namespace Quizzer.Views.HelperViewModels
{
    /// <summary>Eine Zeile der Auswahl: die Regel und ihr Name.</summary>
    public sealed record SonderregelOption(CellSpecialRule Regel, string Name);

    /// <summary>
    /// Sonderregel der Zelle im Auswahldialog (M35 C3): welche Regel, und ob sie am Beamer-Raster
    /// angekuendigt ist (V3). Nur fuer gewoehnliche Fragen - ein Minispiel traegt keine.
    /// </summary>
    public partial class QuestionSelectorViewModel
    {
        /// <summary>„Keine" vorn, dann jede Regel aus den Profilen.</summary>
        public IReadOnlyList<SonderregelOption> SonderregelOptionen { get; } =
        [
            new(CellSpecialRule.None, SpecialRuleProfiles.NameOf(CellSpecialRule.None)),
            .. SpecialRuleProfiles.All.Select(p => new SonderregelOption(p.Rule, p.DisplayName)),
        ];

        /// <summary>Ob diese Zelle eine Regel tragen darf: eine gewoehnliche Frage, noch nicht gespielt.</summary>
        public bool SonderregelMoeglich => Coordinate != null && Sonderregelraster.Erlaubt(Coordinate);

        /// <summary>Warum keine Regel geht - leer, wenn sie geht.</summary>
        public string SonderregelHinweis => Coordinate?.QuestionBase switch
        {
            null => "Erst eine Frage wählen – eine leere Zelle trägt keine Sonderregel.",
            { Typ: QuestionType.Minigame } => "Ein Minispiel trägt keine Sonderregel.",
            _ when Coordinate.IsDone => "Die Zelle ist schon gespielt.",
            _ => string.Empty,
        };

        public SonderregelOption? GewaehlteSonderregel
        {
            get => SonderregelOptionen.FirstOrDefault(o => o.Regel == (Coordinate?.SpecialRule ?? CellSpecialRule.None));
            set
            {
                if (Coordinate == null || value == null)
                    return;

                // Fail-closed: auf einem Minispiel oder einer leeren Zelle bleibt es bei „Keine".
                Coordinate.SpecialRule = value.Regel == CellSpecialRule.None || SonderregelMoeglich
                    ? value.Regel
                    : CellSpecialRule.None;

                if (Coordinate.SpecialRule == CellSpecialRule.None)
                    Coordinate.SpecialRuleAnnounced = false;

                SonderregelGeaendert();
            }
        }

        /// <summary>Angekuendigt: die Gaeste sehen die Regel schon im Raster (V3); ohne Regel immer aus.</summary>
        public bool SonderregelAngekuendigt
        {
            get => Coordinate?.SpecialRuleAnnounced ?? false;
            set
            {
                if (Coordinate == null)
                    return;

                Coordinate.SpecialRuleAnnounced = value && Coordinate.SpecialRule != CellSpecialRule.None;
                SonderregelGeaendert();
            }
        }

        private void SonderregelGeaendert()
        {
            OnPropertyChanged(nameof(GewaehlteSonderregel));
            OnPropertyChanged(nameof(SonderregelAngekuendigt));
            OnPropertyChanged(nameof(SonderregelMoeglich));
            OnPropertyChanged(nameof(SonderregelHinweis));
        }
    }
}
