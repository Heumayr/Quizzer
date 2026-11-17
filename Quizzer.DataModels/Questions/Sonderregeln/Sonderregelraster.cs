using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;

namespace Quizzer.DataModels.Questions.Sonderregeln
{
    /// <summary>Wie viele Regeln wohin, beim zufaelligen Verteilen (V3).</summary>
    /// <param name="Anzahl">So viele Zellen bekommen eine Regel - hoechstens so viele, wie frei sind.</param>
    /// <param name="Angekuendigt">Ob die verteilten Regeln am Beamer-Raster sichtbar sind.</param>
    /// <param name="Regeln">Aus diesen wird je Zelle gewuerfelt.</param>
    /// <param name="Zufall">Wie <see cref="Random.Next(int, int)"/> - eigene Quelle nur fuer Tests.</param>
    public sealed record Verteilauftrag(int Anzahl, bool Angekuendigt, IReadOnlyList<CellSpecialRule> Regeln, Func<int, int, int> Zufall)
    {
        /// <summary>Ob das Spiel ein Teamspiel ist - verteilt wird nur, was dazu passt (M37 T5).</summary>
        public bool Teamspiel { get; init; }
    }

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

            Leere(zelle);
            return true;
        }

        /// <summary>Nimmt jede Regel vom Raster; liefert die Zellen, die eine trugen.</summary>
        public static IReadOnlyList<GameGridCoordinate> Entferne(IEnumerable<GameGridCoordinate> zellen)
        {
            var belegt = zellen.Where(z => z.SpecialRule != CellSpecialRule.None || z.SpecialRuleAnnounced
                || z.SpecialRuleShare != 0 || z.SpecialRuleVariant != 0).ToList();

            foreach (var zelle in belegt)
                Leere(zelle);

            return belegt;
        }

        /// <summary>Keine Regel - und damit weder Ankuendigung noch Anteil noch Spielweise (M35 C3b).</summary>
        public static void Leere(GameGridCoordinate zelle)
        {
            zelle.SpecialRule = CellSpecialRule.None;
            zelle.SpecialRuleAnnounced = false;
            zelle.SpecialRuleShare = 0;
            zelle.SpecialRuleVariant = 0;
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

            // M37 T5, fail-closed: nur bekannte Regeln, die zum Spiel mit oder ohne Teams passen. Das Finale (M37 E4) setzt
            // der Spielleiter selbst - gewuerfelt laege es irgendwo im Raster, und zwei waeren moeglich.
            var regeln = auftrag.Regeln
                .Where(r => SpecialRuleProfiles.TryFor(r, out var profil) && profil.PasstZu(auftrag.Teamspiel) && !profil.EinmalJeSpiel)
                .ToList();

            if (regeln.Count == 0)
                return belegt;

            while (belegt.Count < auftrag.Anzahl && frei.Count > 0)
            {
                var zelle = frei[auftrag.Zufall(0, frei.Count)];
                frei.Remove(zelle);

                Leere(zelle);
                zelle.SpecialRule = regeln[auftrag.Zufall(0, regeln.Count)];
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

        /// <summary>
        /// Hinweis zum Spielstart: eine Regel auf einer Zelle, auf der sie nicht gespielt wird (V2) - auch eine, die nicht
        /// zum Spiel mit oder ohne Teams passt (M37 T5).
        /// </summary>
        public static ValidationIssue? Startbefund(GameGridCoordinate zelle, Models.QuestionBase? frage, bool teamspiel)
        {
            if (zelle.SpecialRule == CellSpecialRule.None || frage == null || SpecialRuleProfiles.Applies(zelle, frage, teamspiel))
                return null;

            var wo = $"Zelle {zelle.X + 1}/{zelle.Y + 1}";

            if (!SpecialRuleProfiles.TryFor(zelle.SpecialRule, out var regel))
                return new(UnknownRuleCode, ValidationSeverity.Warning,
                    $"{wo}: unbekannte Sonderregel ({(int)zelle.SpecialRule}) – vermutlich aus einer neueren Fassung, sie wird nicht gespielt.");

            if (frage.Typ == QuestionType.Minigame)
                return new(MinigameRuleCode, ValidationSeverity.Warning,
                    $"{wo}: Sonderregel „{regel.DisplayName}“ auf einem Minispiel – sie wird nicht gespielt.");

            return new(TeamGameRuleCode, ValidationSeverity.Warning, regel.IstTeamregel
                ? $"{wo}: Teamregel „{regel.DisplayName}“ gibt es nur im Teamspiel – sie wird nicht gespielt."
                : $"{wo}: Sonderregel „{regel.DisplayName}“ wird im Teamspiel nicht gespielt.");
        }

        public const string MinigameRuleCode = "special-rule-on-minigame";
        /// <summary>
        /// Hinweis zum Spielstart über das ganze Raster (M37 E4): eine Regel, die es nur einmal je Spiel gibt, liegt mehrfach -
        /// aus einem Bündel oder dem Abgleich. Gespielt wird jede; die Spielleitung entscheidet (E1).
        /// </summary>
        public static IReadOnlyList<ValidationIssue> Spielbefunde(IEnumerable<GameGridCoordinate> zellen)
            => zellen
                .Where(z => SpecialRuleProfiles.TryFor(z.SpecialRule, out var profil) && profil.EinmalJeSpiel)
                .GroupBy(z => z.SpecialRule)
                .Where(g => g.Count() > 1)
                .Select(g => new ValidationIssue(OnlyOncePerGameCode, ValidationSeverity.Warning,
                    $"„{SpecialRuleProfiles.NameOf(g.Key)}“ liegt {g.Count()}-mal im Raster ("
                    + string.Join(", ", g.OrderBy(z => z.X).ThenBy(z => z.Y).Select(z => $"Zelle {z.X + 1}/{z.Y + 1}"))
                    + ") – gedacht ist es einmal je Spiel, für die letzte Frage."))
                .ToList();

        /// <summary>Ob eine Zelle die Regel tragen darf, ohne dass sie im Spiel doppelt liegt (M37 E4: das Finale).</summary>
        public static bool FreiFuer(CellSpecialRule regel, GameGridCoordinate zelle, IEnumerable<GameGridCoordinate> raster)
            => !SpecialRuleProfiles.TryFor(regel, out var profil) || !profil.EinmalJeSpiel
               || !raster.Any(z => z.SpecialRule == regel && !DieselbeZelle(z, zelle));

        /// <summary>Dieselbe Zelle - auch als Klon mit derselben Kennung; zwei ungespeicherte sind verschieden.</summary>
        private static bool DieselbeZelle(GameGridCoordinate a, GameGridCoordinate b)
            => ReferenceEquals(a, b) || (a.Id != Guid.Empty && a.Id == b.Id);

        public const string UnknownRuleCode = "special-rule-unknown";
        public const string TeamGameRuleCode = "special-rule-team-game";
        public const string OnlyOncePerGameCode = "special-rule-once-per-game";
    }
}
