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

                foreach (var schritt in frage.Schritte)
                {
                    if (schritt.MedienSchluessel != null
                        && !medienSchluessel.Contains(schritt.MedienSchluessel))
                        throw new InvalidDataException(
                            $"Frage {i + 1} verweist auf ein Medium, das im Bündel fehlt.");
                }
            }

            foreach (var zelle in dokument.Zellen)
            {
                if (zelle.FrageIndex is int index
                    && (index < 0 || index >= dokument.Fragen.Count))
                    throw new InvalidDataException(
                        $"Die Zelle {zelle.X}/{zelle.Y} zeigt auf eine Frage, die es im Bündel "
                        + "nicht gibt.");
            }

            foreach (var textur in dokument.Design?.Texturen ?? new Dictionary<string, string>())
            {
                if (!medienSchluessel.Contains(textur.Value))
                    throw new InvalidDataException(
                        $"Das Design verweist auf eine Textur, die im Bündel fehlt ({textur.Key}).");
            }
        }
    }
}
