using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;

namespace Quizzer.DataModels.Questions.Sonderregeln
{
    /// <summary>Wie viele Regeln wohin, beim zufaelligen Verteilen (V3).</summary>
    /// <param name="Anzahl">So viele Zellen bekommen eine Regel - hoechstens so viele, wie frei sind.</param>
    /// <param name="Angekuendigt">Ob die verteilten Regeln am Beamer-Raster sichtbar sind.</param>
    /// <param name="Regeln">Aus diesen wird je Zelle gewuerfelt.</param>
    /// <param name="Zufall">Wie <see cref="Random.Next(int, int)"/> - eigene Quelle nur fuer Tests.</param>
    public sealed record Verteilauftrag(int Anzahl, bool Angekuendigt, IReadOnlyList<CellSpecialRule> Regeln, Func<int, int, int> Zufall);

    /// <summary>
    /// Sonderregeln im Raster (M35 C3, V3): wo eine liegen darf, das zufaellige Verteilen und die Marke
    /// fuer Spielleiter- und Beamer-Raster.
    /// </summary>
    public static class Sonderregelraster
    {
        /// <summary>
        /// Ob eine Zelle eine Regel tragen darf: eine gewoehnliche Frage, noch nicht gespielt. Ohne
        /// geladene Frage ist der Typ unbekannt - dann nicht (fail-closed).
        /// </summary>
        public static bool Erlaubt(GameGridCoordinate zelle)
            => zelle.QuestionBase is { } frage && frage.Typ != QuestionType.Minigame && !zelle.IsDone;

        /// <summary>Wie viele Zellen schon eine Regel tragen.</summary>
        public static int Belegt(IEnumerable<GameGridCoordinate> zellen)
            => zellen.Count(z => z.SpecialRule != CellSpecialRule.None);

        /// <summary>
        /// Nimmt eine Regel von einer Zelle, auf der sie nie gespielt wuerde - Minispiel oder leer (beim
        /// Speichern im Spielaufbau). Ist die Frage nicht geladen, bleibt sie: das prueft der Spielstart.
        /// </summary>
        public static bool Bereinige(GameGridCoordinate zelle)
        {
            if (zelle.SpecialRule == CellSpecialRule.None
                || (zelle.HatFrage && zelle.QuestionBase?.Typ != QuestionType.Minigame))
                return false;

            zelle.SpecialRule = CellSpecialRule.None;
            zelle.SpecialRuleAnnounced = false;
            return true;
        }

        /// <summary>Nimmt jede Regel vom Raster; liefert die Zellen, die eine trugen.</summary>
        public static IReadOnlyList<GameGridCoordinate> Entferne(IEnumerable<GameGridCoordinate> zellen)
        {
            var belegt = zellen.Where(z => z.SpecialRule != CellSpecialRule.None || z.SpecialRuleAnnounced).ToList();

            foreach (var zelle in belegt)
            {
                zelle.SpecialRule = CellSpecialRule.None;
                zelle.SpecialRuleAnnounced = false;
            }

            return belegt;
        }

        /// <summary>
        /// Verteilt die Regeln zufaellig auf freie, erlaubte Zellen - eine Zelle mit Regel bleibt, wie
        /// sie ist. Liefert die neu belegten Zellen.
        /// </summary>
        public static IReadOnlyList<GameGridCoordinate> Verteile(IEnumerable<GameGridCoordinate> zellen, Verteilauftrag auftrag)
        {
            ArgumentNullException.ThrowIfNull(auftrag);

            var frei = zellen.Where(z => Erlaubt(z) && z.SpecialRule == CellSpecialRule.None).ToList();
            var belegt = new List<GameGridCoordinate>();

            if (auftrag.Regeln.Count == 0)
                return belegt;

            while (belegt.Count < auftrag.Anzahl && frei.Count > 0)
            {
                var zelle = frei[auftrag.Zufall(0, frei.Count)];
                frei.Remove(zelle);

                zelle.SpecialRule = auftrag.Regeln[auftrag.Zufall(0, auftrag.Regeln.Count)];
                zelle.SpecialRuleAnnounced = auftrag.Angekuendigt;
                belegt.Add(zelle);
            }

            return belegt;
        }

        /// <summary>Die Marke im Spielleiter-Raster: jede Regel, mit Hinweis, ob die Gaeste sie sehen.</summary>
        public static string MarkeSpielleiter(GameGridCoordinate zelle)
            => zelle.SpecialRule == CellSpecialRule.None
                ? string.Empty
                : $"{SpecialRuleProfiles.Symbol} {SpecialRuleProfiles.NameOf(zelle.SpecialRule)} · "
                  + (zelle.SpecialRuleAnnounced ? "angekündigt" : "verdeckt");

        /// <summary>Die Marke am Beamer-Raster: nur eine angekuendigte Regel - eine verdeckte verraet nichts.</summary>
        public static string MarkeBeamer(GameGridCoordinate zelle)
            => zelle.SpecialRule == CellSpecialRule.None || !zelle.SpecialRuleAnnounced
                ? string.Empty
                : $"{SpecialRuleProfiles.Symbol} {SpecialRuleProfiles.NameOf(zelle.SpecialRule)}";

        /// <summary>Hinweis zum Spielstart: eine Regel auf einer Zelle, auf der sie nicht gespielt wird (V2).</summary>
        public static ValidationIssue? Startbefund(GameGridCoordinate zelle, Models.QuestionBase? frage)
        {
            if (zelle.SpecialRule == CellSpecialRule.None || frage == null || SpecialRuleProfiles.Applies(zelle, frage))
                return null;

            var wo = $"Zelle {zelle.X + 1}/{zelle.Y + 1}";

            return SpecialRuleProfiles.TryFor(zelle.SpecialRule, out var regel)
                ? new(MinigameRuleCode, ValidationSeverity.Warning,
                    $"{wo}: Sonderregel „{regel.DisplayName}“ auf einem Minispiel – sie wird nicht gespielt.")
                : new(UnknownRuleCode, ValidationSeverity.Warning,
                    $"{wo}: unbekannte Sonderregel ({(int)zelle.SpecialRule}) – vermutlich aus einer neueren Fassung, sie wird nicht gespielt.");
        }

        public const string MinigameRuleCode = "special-rule-on-minigame";
        public const string UnknownRuleCode = "special-rule-unknown";
    }
}
