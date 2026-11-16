using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models;

namespace Quizzer.DataModels.Questions.Sonderregeln
{
    /// <summary>Eine Spielweise einer Sonderregel (M35 C3b), etwa „Zweitpreis" bei der Auktion.</summary>
    /// <param name="Name">Im Auswahldialog.</param>
    /// <param name="RulesText">Der Regeltext dieser Spielweise; <see cref="SpecialRuleProfiles.SharePlaceholder"/> wird ersetzt.</param>
    public sealed record SpecialRuleVariant(string Name, string RulesText);

    /// <summary>Was eine Sonderregel ausmacht - Name und Regeltext fuer Beamer, Telefon und Spielaufbau.</summary>
    /// <param name="Rule">Der Wert an der Zelle.</param>
    /// <param name="DisplayName">„Einsatz" - im Spielaufbau, auf der Marke und am Beamer.</param>
    /// <param name="RulesText">Der Regeltext, den Beamer und Telefon in der Vorphase zeigen - ohne Spielweisen.</param>
    public sealed record SpecialRuleProfile(CellSpecialRule Rule, string DisplayName, string RulesText)
    {
        /// <summary>Anteil in Prozent der Feldpunkte, wenn die Zelle keinen eigenen traegt; 0: die Regel kennt keinen.</summary>
        public int DefaultShare { get; init; }

        /// <summary>Ob der Spielaufbau ein Feld „Anteil %" zeigt.</summary>
        public bool ShowShare => DefaultShare > 0;

        /// <summary>Die Spielweisen, Index = <see cref="GameGridCoordinate.SpecialRuleVariant"/>; leer: nur eine.</summary>
        public IReadOnlyList<SpecialRuleVariant> Variants { get; init; } = [];

        /// <summary>Ob der Spielaufbau eine Spielweise anbietet.</summary>
        public bool ShowVariant => Variants.Count > 1;

        /// <summary>Ob die Regel eine Vorphase am Telefon spielt - das Kopfgeld nicht, es steht beim Oeffnen fest.</summary>
        public bool HasPrePhase { get; init; } = true;

        /// <summary>Der Regeltext der Spielweise mit eingesetztem Anteil; eine unbekannte Spielweise liest sich wie die erste.</summary>
        public string RulesTextFor(int variant, int share)
        {
            var text = Variants.Count == 0 ? RulesText
                : variant >= 0 && variant < Variants.Count ? Variants[variant].RulesText
                : Variants[0].RulesText;

            return text.Replace(SpecialRuleProfiles.SharePlaceholder, share.ToString());
        }
    }

    /// <summary>
    /// Die Sonderregeln (M35 C3) - die einzige Quelle fuer Name, Regeltext, Anteil und Spielweisen. Eine neue
    /// Regel bringt hier eine Zeile, am Server ihre Vorphase und im Fragefenster ihre Wertung.
    /// </summary>
    public static class SpecialRuleProfiles
    {
        /// <summary>Was die Marke vor den Namen setzt - nie allein Farbe.</summary>
        public const string Symbol = "⚑";

        /// <summary>Steht im Regeltext fuer den Anteil der Zelle - <see cref="SpecialRuleProfile.RulesTextFor"/> setzt die Zahl ein.</summary>
        public const string SharePlaceholder = "{Anteil}";

        /// <summary>Ein eigener Anteil an der Zelle: 1 bis 100 Prozent; 0 heisst Vorgabe.</summary>
        public const int MaxShare = 100;

        public static IReadOnlyList<SpecialRuleProfile> All { get; } =
        [
            new(CellSpecialRule.Stake, "Einsatz",
                "Vor der Frage setzt jeder am Telefon Punkte – höchstens seinen Kontostand oder die Feldpunkte, "
                + "je nachdem, was mehr ist. Richtig gewinnt den Einsatz, falsch verliert ihn. "
                + "Wer nichts setzt, spielt um die Feldpunkte."),
            new(CellSpecialRule.Risk, "Risiko",
                "Vor der Frage entscheidet jeder am Telefon: doppelt spielen? Wer ja wählt, bekommt Plus und "
                + "Minus doppelt. Wer nein wählt oder nichts, spielt wie immer."),
        ];

        public static bool TryFor(CellSpecialRule rule, out SpecialRuleProfile profile)
        {
            profile = All.FirstOrDefault(p => p.Rule == rule)!;
            return profile != null;
        }

        /// <summary>Der Name der Regel; „Keine" ohne Regel, die Zahl bei einer unbekannten.</summary>
        public static string NameOf(CellSpecialRule rule)
            => rule == CellSpecialRule.None ? "Keine"
                : TryFor(rule, out var profil) ? profil.DisplayName
                : $"Unbekannt ({(int)rule})";

        /// <summary>Der Anteil, mit dem die Zelle spielt: ihr eigener, sonst die Vorgabe der Regel; 0 bei keiner.</summary>
        public static int ShareOf(GameGridCoordinate zelle)
            => zelle.SpecialRuleShare > 0 ? zelle.SpecialRuleShare
                : TryFor(zelle.SpecialRule, out var profil) ? profil.DefaultShare
                : 0;

        /// <summary>Der Regeltext der Zelle - Spielweise und Anteil eingesetzt; leer ohne bekannte Regel.</summary>
        public static string RulesTextOf(GameGridCoordinate zelle)
            => TryFor(zelle.SpecialRule, out var profil) ? profil.RulesTextFor(zelle.SpecialRuleVariant, ShareOf(zelle)) : string.Empty;

        /// <summary>
        /// Ob die Regel dieser Zelle gespielt wird: bekannt, und die Frage ist eine gewoehnliche. Ein
        /// Minispiel spielt nie mit Sonderregel - fail-closed auch, wenn der Typ nicht geladen ist.
        /// </summary>
        public static bool Applies(GameGridCoordinate zelle, QuestionBase? frage)
            => zelle.SpecialRule != CellSpecialRule.None
               && TryFor(zelle.SpecialRule, out _)
               && frage != null
               && frage.Typ != QuestionType.Minigame;
    }
}
