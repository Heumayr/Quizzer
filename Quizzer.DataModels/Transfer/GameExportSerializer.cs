using Quizzer.DataModels.Questions;
using Quizzer.DataModels.Questions.Sonderregeln;
using Quizzer.DataModels.Questions.Teams;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Quizzer.DataModels.Transfer
{
    /// <summary>
    /// Schreibt und liest <see cref="GameExportDocument"/> als JSON.
    /// <para>
    /// <b>Mit eigenen Einstellungen, nicht mit denen des Projekts.</b> Die
    /// <c>DefaultSettings</c> in <c>ExtentionMethods</c> stehen auf
    /// <c>MissingMemberHandling.Ignore</c> - ein falsch gebautes Dokument käme damit still durch
    /// und ergäbe ein halbes Spiel.
    /// </para>
    /// <para>
    /// <b>Unbekannte Felder sind trotzdem erlaubt</b>, und das ist kein Widerspruch: eine Datei,
    /// die eine neuere Fassung geschrieben hat, soll lesbar bleiben. Geprüft wird deshalb nicht
    /// die Feldliste, sondern die <b>Struktur</b> - und zwar ausdrücklich in
    /// <see cref="Pruefe"/>.
    /// </para>
    /// </summary>
    public static class GameExportSerializer
    {
        /// <summary>Der Name des Dokuments im Bündel.</summary>
        public const string DocumentEntryName = "spiel.json";

        /// <summary>Der Ordner im Bündel, in dem die Medien liegen.</summary>
        public const string MediaFolderName = "medien";

        /// <summary>Die höchste Formatversion, die dieser Stand lesen kann.</summary>
        public const int SupportedFormatVersion = 1;

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            PropertyNamingPolicy = null,
        };

        public static void Write(Stream ziel, GameExportDocument dokument)
            => JsonSerializer.Serialize(ziel, dokument, Options);

        /// <summary>Nur für Zusicherungen und für die Fehlersuche - dasselbe als Zeichenfolge.</summary>
        public static string ToJson(GameExportDocument dokument)
            => JsonSerializer.Serialize(dokument, Options);

        /// <summary>
        /// Liest ein Dokument und prüft es, bevor es zurückkommt.
        /// </summary>
        /// <exception cref="InvalidDataException">
        /// Wenn die Datei kein lesbares Dokument ist oder die Struktur nicht stimmt.
        /// </exception>
        public static GameExportDocument Read(Stream quelle)
        {
            GameExportDocument? dokument;

            try
            {
                dokument = JsonSerializer.Deserialize<GameExportDocument>(quelle, Options);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException(
                    "Die Datei ist kein lesbares Quizzer-Bündel: " + ex.Message, ex);
            }

            if (dokument == null)
                throw new InvalidDataException("Die Datei ist leer.");

            Pruefe(dokument);

            return dokument;
        }

        /// <summary>
        /// Prüft, ob das Dokument in sich stimmig ist - Version, Verweise, Pflichtangaben.
        /// <para>
        /// <b>Ein Dokument aus fremder Hand ist Material, keine Anweisung.</b> Jeder Verweis wird
        /// geprüft, bevor irgendetwas damit geschieht; ein Index, der ins Leere zeigt, würde beim
        /// Import sonst entweder werfen oder - schlimmer - stillschweigend die falsche Frage auf
        /// eine Zelle legen.
        /// </para>
        /// </summary>
        /// <exception cref="InvalidDataException">Mit einem Text, der die Stelle benennt.</exception>
        public static void Pruefe(GameExportDocument dokument)
        {
            if (dokument.FormatVersion > SupportedFormatVersion)
                throw new InvalidDataException(
                    $"Das Bündel wurde mit einer neueren Fassung erstellt (Format "
                    + $"{dokument.FormatVersion}, lesbar bis {SupportedFormatVersion}).");

            if (dokument.Spiel == null)
                throw new InvalidDataException("Im Bündel fehlt das Spiel.");

            var medienSchluessel = new HashSet<string>(
                dokument.Medien.Select(m => m.Schluessel), StringComparer.Ordinal);

            for (var i = 0; i < dokument.Fragen.Count; i++)
            {
                var frage = dokument.Fragen[i];

                if (frage.KategorieIndex < 0 || frage.KategorieIndex >= dokument.Kategorien.Count)
                    throw new InvalidDataException(
                        $"Frage {i + 1} zeigt auf eine Kategorie, die es im Bündel nicht gibt.");

                if (!Enum.IsDefined(frage.Typ))
                    throw new InvalidDataException(
                        $"Frage {i + 1} hat einen unbekannten Fragetyp ({(int)frage.Typ}). "
                        + "Vermutlich stammt das Bündel aus einer neueren Fassung.");

                if (frage.Typeigen?.MinigameKind is { } art && !Enum.IsDefined(art))
                    throw new InvalidDataException(
                        $"Frage {i + 1} ist ein Minispiel unbekannter Art ({(int)art}). "
                        + "Vermutlich stammt das Bündel aus einer neueren Fassung.");

                foreach (var schritt in frage.Schritte)
                {
                    if (schritt.MedienSchluessel != null
                        && !medienSchluessel.Contains(schritt.MedienSchluessel))
                        throw new InvalidDataException(
                            $"Frage {i + 1} verweist auf ein Medium, das im Bündel fehlt.");
                }

                if (frage.Typeigen?.BildSchluessel is string bild
                    && !medienSchluessel.Contains(bild))
                    throw new InvalidDataException(
                        $"Frage {i + 1} verweist auf ein Bild, das im Bündel fehlt.");
            }

            foreach (var zelle in dokument.Zellen)
            {
                if (zelle.FrageIndex is int index
                    && (index < 0 || index >= dokument.Fragen.Count))
                    throw new InvalidDataException(
                        $"Die Zelle {zelle.X}/{zelle.Y} zeigt auf eine Frage, die es im Bündel "
                        + "nicht gibt.");

                if (!Enum.IsDefined(zelle.SpecialRule))
                    throw new InvalidDataException(
                        $"Die Zelle {zelle.X}/{zelle.Y} trägt eine unbekannte Sonderregel ({(int)zelle.SpecialRule}). "
                        + "Vermutlich stammt das Bündel aus einer neueren Fassung.");

                if (zelle.SpecialRuleShare < 0 || zelle.SpecialRuleShare > SpecialRuleProfiles.MaxShare)
                    throw new InvalidDataException(
                        $"Die Zelle {zelle.X}/{zelle.Y} trägt einen Anteil von {zelle.SpecialRuleShare} % – erlaubt sind 0 bis {SpecialRuleProfiles.MaxShare}.");

                if (zelle.SpecialRuleVariant < 0)
                    throw new InvalidDataException(
                        $"Die Zelle {zelle.X}/{zelle.Y} trägt eine ungültige Spielweise ({zelle.SpecialRuleVariant}).");
            }

            foreach (var textur in dokument.Design?.Texturen ?? new Dictionary<string, string>())
            {
                if (!medienSchluessel.Contains(textur.Value))
                    throw new InvalidDataException(
                        $"Das Design verweist auf eine Textur, die im Bündel fehlt ({textur.Key}).");
            }

            PruefeTeams(dokument);
            PruefePunkte(dokument);
            PruefeRaster(dokument.Spiel);
        }

        /// <summary>P5-4: Raster und Phasen nie negativ - nach <see cref="Rastergrenzen"/>, wie am Server und in der Maske.</summary>
        private static void PruefeRaster(GameExportDocument.GameData spiel)
        {
            var werte = new Dictionary<string, double>
            {
                [nameof(spiel.Height)] = spiel.Height,
                [nameof(spiel.Width)] = spiel.Width,
                [nameof(spiel.CellHeight)] = spiel.CellHeight,
                [nameof(spiel.CellWidth)] = spiel.CellWidth,
                [nameof(spiel.SuggestedPhases)] = spiel.SuggestedPhases,
            };

            // Ein Feld, das hier fehlt, wirft (KeyNotFoundException) - nie still ungeprüft.
            foreach (var (feld, beschriftung) in Rastergrenzen.SpielFelder)
            {
                if (Punktegrenzen.Problem(beschriftung, werte[feld]) is { } problem)
                    throw new InvalidDataException($"Spiel: {problem}");
            }
        }

        /// <summary>R3-3: Punkte-Parameter nie negativ - nach <see cref="Punktegrenzen"/>, wie am Server und in der Maske.</summary>
        private static void PruefePunkte(GameExportDocument dokument)
        {
            var spiel = dokument.Spiel;
            var werte = new Dictionary<string, double>
            {
                [nameof(spiel.DifficultyMultiplier)] = spiel.DifficultyMultiplier,
                [nameof(spiel.DifficultyAddition)] = spiel.DifficultyAddition,
                [nameof(spiel.DifficultyMinusMultiplier)] = spiel.DifficultyMinusMultiplier,
                [nameof(spiel.DifficultyMinusAddition)] = spiel.DifficultyMinusAddition,
                [nameof(spiel.PhaseMultiplier)] = spiel.PhaseMultiplier,
                [nameof(spiel.PhaseAddition)] = spiel.PhaseAddition,
            };

            // Ein Feld, das hier fehlt, wirft (KeyNotFoundException) - nie still ungeprüft.
            foreach (var (feld, beschriftung) in Punktegrenzen.SpielFelder)
            {
                if (Punktegrenzen.Problem(beschriftung, werte[feld]) is { } problem)
                    throw new InvalidDataException($"Spiel: {problem}");
            }

            for (var i = 0; i < dokument.Fragen.Count; i++)
            {
                var frage = dokument.Fragen[i];

                if ((Punktegrenzen.Problem(Punktegrenzen.FrageFelder[nameof(frage.Points)], frage.Points)
                    ?? Punktegrenzen.Problem(Punktegrenzen.FrageFelder[nameof(frage.MinusPoints)], frage.MinusPoints)) is { } problem)
                    throw new InvalidDataException($"Frage {i + 1}: {problem}");
            }
        }

        /// <summary>Teameinstellungen und Teams (M37 T1) nach denselben Grenzen wie am Server (<see cref="TeamGrenzen"/>).</summary>
        private static void PruefeTeams(GameExportDocument dokument)
        {
            if (TeamGrenzen.SettingsProblem(dokument.Spiel.TeamMinigameScoring, dokument.Spiel.TeamCouncilSeconds) is { } spiel)
                throw new InvalidDataException(spiel);

            for (var i = 0; i < dokument.Teams.Count; i++)
            {
                var team = dokument.Teams[i];

                if (TeamGrenzen.TeamProblem(team.Name, team.ColorIndex, speakerTurn: 0) is { } problem)
                    throw new InvalidDataException($"Team {i + 1}: {problem}");
            }

            // R1-7: die Aufstellung als Ganzes, wie SaveLineupAsync - höchstens acht, keine Farbe und kein Name doppelt.
            var aufstellung = dokument.Teams.Select(t => new Models.Base.GameTeam { Name = t.Name, ColorIndex = t.ColorIndex }).ToList();

            if (TeamGrenzen.AufstellungProblem(aufstellung) is { } ganzes)
                throw new InvalidDataException(ganzes);
        }
    }
}
