using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Sonderregeln;
using System.Windows;

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
        /// <summary>„Keine" vorn, dann jede Regel aus den Profilen, die zum Spiel mit oder ohne Teams passt (M37 T5).</summary>
        public IReadOnlyList<SonderregelOption> SonderregelOptionen =>
        [
            new(CellSpecialRule.None, SpecialRuleProfiles.NameOf(CellSpecialRule.None)),
            .. SpecialRuleProfiles.Angeboten(Teamspiel).Select(p => new SonderregelOption(p.Rule, p.DisplayName)),
        ];

        private bool Teamspiel => Game?.IsTeamGame == true;

        /// <summary>Ob diese Zelle eine Regel tragen darf: eine gewoehnliche Frage, noch nicht gespielt.</summary>
        public bool SonderregelMoeglich => Coordinate != null && Sonderregelraster.Erlaubt(Coordinate);

        /// <summary>Warum keine Regel geht - leer, wenn sie geht.</summary>
        public string SonderregelHinweis => Coordinate?.QuestionBase switch
        {
            null => "Erst eine Frage wählen – eine leere Zelle trägt keine Sonderregel.",
            { Typ: QuestionType.Minigame } => "Ein Minispiel trägt keine Sonderregel.",
            _ when Coordinate.IsDone => "Die Zelle ist schon gespielt.",
            _ when Profil is { } profil && !profil.PasstZu(Teamspiel) => profil.IstTeamregel
                ? $"„{profil.DisplayName}“ gibt es nur im Teamspiel – sie wird nicht gespielt."
                : $"„{profil.DisplayName}“ wird im Teamspiel nicht gespielt.",
            _ => string.Empty,
        };

        public SonderregelOption? GewaehlteSonderregel
        {
            get => SonderregelOptionen.FirstOrDefault(o => o.Regel == (Coordinate?.SpecialRule ?? CellSpecialRule.None));
            set
            {
                if (Coordinate == null || value == null)
                    return;

                if (value.Regel == Coordinate.SpecialRule)
                    return;

                // Fail-closed: auf einem Minispiel, einer leeren Zelle oder mit einer Regel, die nicht zum Spiel passt, bleibt es bei „Keine".
                var regel = value.Regel == CellSpecialRule.None || (SonderregelMoeglich && SonderregelOptionen.Contains(value)) ? value.Regel : CellSpecialRule.None;
                var angekuendigt = Coordinate.SpecialRuleAnnounced && regel != CellSpecialRule.None;

                // Eine andere Regel beginnt mit ihrer Vorgabe: Anteil und Spielweise gehoerten der alten.
                Sonderregelraster.Leere(Coordinate);
                Coordinate.SpecialRule = regel;
                Coordinate.SpecialRuleAnnounced = angekuendigt;

                SonderregelGeaendert();
            }
        }

        private SpecialRuleProfile? Profil
            => Coordinate != null && SpecialRuleProfiles.TryFor(Coordinate.SpecialRule, out var profil) ? profil : null;

        /// <summary>Eigener Anteil der Zelle in Prozent; 0 heisst Vorgabe der Regel. Ausserhalb 0 bis 100 bleibt der alte.</summary>
        public int SonderregelAnteil
        {
            get => Coordinate?.SpecialRuleShare ?? 0;
            set
            {
                if (Coordinate == null || Profil is not { ShowShare: true } || value < 0 || value > SpecialRuleProfiles.MaxShare)
                {
                    OnPropertyChanged(nameof(SonderregelAnteil));
                    return;
                }

                Coordinate.SpecialRuleShare = value;
                SonderregelGeaendert();
            }
        }

        public Visibility SonderregelAnteilVisibility => Profil is { ShowShare: true } ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Die Spielweisen der gewaehlten Regel; leer, wenn sie nur eine kennt.</summary>
        public IReadOnlyList<SpecialRuleVariant> SonderregelSpielweisen => Profil?.Variants ?? [];

        /// <summary>Index der Spielweise an der Zelle.</summary>
        public int SonderregelSpielweise
        {
            get => Coordinate?.SpecialRuleVariant ?? 0;
            set
            {
                if (Coordinate == null || value < 0 || value >= SonderregelSpielweisen.Count)
                    return;

                Coordinate.SpecialRuleVariant = value;
                SonderregelGeaendert();
            }
        }

        public Visibility SonderregelSpielweiseVisibility => Profil is { ShowVariant: true } ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Der Regeltext, wie ihn Beamer und Telefon zeigen werden - Anteil und Spielweise eingesetzt.</summary>
        public string SonderregelRegeltext => Coordinate == null ? string.Empty : SpecialRuleProfiles.RulesTextOf(Coordinate);

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
            OnPropertyChanged(nameof(SonderregelOptionen));
            OnPropertyChanged(nameof(GewaehlteSonderregel));
            OnPropertyChanged(nameof(SonderregelAngekuendigt));
            OnPropertyChanged(nameof(SonderregelMoeglich));
            OnPropertyChanged(nameof(SonderregelHinweis));
            OnPropertyChanged(nameof(SonderregelAnteil));
            OnPropertyChanged(nameof(SonderregelAnteilVisibility));
            OnPropertyChanged(nameof(SonderregelSpielweisen));
            OnPropertyChanged(nameof(SonderregelSpielweise));
            OnPropertyChanged(nameof(SonderregelSpielweiseVisibility));
            OnPropertyChanged(nameof(SonderregelRegeltext));
        }
    }
}
