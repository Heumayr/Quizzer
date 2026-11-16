using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models;

namespace Quizzer.DataModels.Questions.Sonderregeln
{
    /// <summary>Was eine Sonderregel ausmacht - Name und Regeltext fuer Beamer, Telefon und Spielaufbau.</summary>
    /// <param name="Rule">Der Wert an der Zelle.</param>
    /// <param name="DisplayName">„Einsatz" - im Spielaufbau, auf der Marke und am Beamer.</param>
    /// <param name="RulesText">Der Regeltext, den Beamer und Telefon in der Vorphase zeigen.</param>
    public sealed record SpecialRuleProfile(CellSpecialRule Rule, string DisplayName, string RulesText);

    /// <summary>
    /// Die Sonderregeln (M35 C3) - die einzige Quelle fuer Name und Regeltext. Eine neue Regel (C3b)
    /// bringt hier eine Zeile, am Server ihre Vorphase und im Fragefenster ihre Wertung.
    /// </summary>
    public static class SpecialRuleProfiles
    {
        /// <summary>Was die Marke vor den Namen setzt - nie allein Farbe.</summary>
        public const string Symbol = "⚑";

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
