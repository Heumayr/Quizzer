using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer;
using Quizzer.Logic.Controller.TypedControllers;
using System.IO.Compression;

namespace Quizzer.Logic.Transfer
{
    /// <summary>
    /// Schreibt ein Spiel als Bündel - eine Zip-Datei mit <c>spiel.json</c> und den Medien
    /// daneben.
    /// <para>
    /// <b>Nutzerwort vom 2026-09-06:</b> „die medien können auch neben dem json liegen aber
    /// müssen quasi mitgenommen werden". Ein Ordner ginge auseinander, sobald jemand nur die
    /// JSON kopiert - deshalb ein Bündel, das eine Datei bleibt.
    /// </para>
    /// </summary>
    public static class GameExporter
    {
        /// <summary>Die Endung, unter der ein Bündel angeboten wird.</summary>
        public const string Extension = ".quizzer";

        /// <summary>Was der Export mitgenommen hat.</summary>
        /// <param name="Fragen">Anzahl exportierter Fragen.</param>
        /// <param name="Medien">Anzahl mitgenommener Mediendateien.</param>
        /// <param name="FehlendeMedien">
        /// Medien, die die Frage nennt, die aber im Datenordner nicht liegen - der Empfänger
        /// bekommt sie nicht, und der Absender soll es erfahren.
        /// </param>
        /// <param name="Groesse">Größe der geschriebenen Datei in Byte.</param>
        public sealed record Ergebnis(int Fragen, int Medien, List<string> FehlendeMedien, long Groesse);

        /// <summary>
        /// Schreibt das Spiel in eine Bündeldatei.
        /// </summary>
        /// <param name="spielId">Das Spiel.</param>
        /// <param name="zieldatei">Wohin geschrieben wird - der Aufrufer wählt den Pfad.</param>
        /// <param name="tresor">Woher die Medien kommen.</param>
        /// <param name="mitNotizen">
        /// Ob die internen Notizen an Fragen und Design mitgehen. Ein Bündel geht an einen
        /// anderen Menschen; wer sein eigenes Spiel umzieht, will sie aber behalten.
        /// </param>
        public static async Task<Ergebnis> ExportAsync(
            Guid spielId, string zieldatei, IMediaVault tresor, bool mitNotizen = true)
        {
            using var ctrlSpiele = new GamesController();

            var spiel = await ctrlSpiele.GetAsync(spielId)
                ?? throw new InvalidOperationException("Das Spiel wurde nicht gefunden.");

            // Die Fragen einzeln nachladen: GetAllAsync bringt die Schritte nicht mit, und ohne
            // sie waere das Buendel ein Spiel ohne Inhalt.
            var fragen = new List<QuestionBase>();

            using (var ctrlFragen = new QuestionBasesController(ctrlSpiele))
            {
                foreach (var id in spiel.GameGridCoordinates
                             .Where(c => c.QuestionBaseId.HasValue)
                             .Select(c => c.QuestionBaseId!.Value)
                             .Distinct())
                {
                    var frage = await ctrlFragen.GetAsync(id);

                    if (frage != null)
                        fragen.Add(frage);
                }
            }

            var kategorien = new List<Category>();

            using (var ctrlKategorien = new CategoriesController(ctrlSpiele))
            {
                var alle = await ctrlKategorien.GetAllAsync();
                var gebraucht = fragen.Select(f => f.CategoryId).ToHashSet();

                kategorien.AddRange(alle.Where(k => gebraucht.Contains(k.Id)));
            }

            GameTheme? design = null;

            if (spiel.GameThemeId is Guid designId)
            {
                using var ctrlDesign = new GameThemesController(ctrlSpiele);

                design = await ctrlDesign.GetAsync(designId);
            }

            if (!mitNotizen)
            {
                foreach (var frage in fragen)
                    frage.Notes = string.Empty;

                if (design != null)
                    design.Notes = string.Empty;
            }

            // Die Medien einsammeln, waehrend das Dokument entsteht.
            var medien = new Dictionary<string, GameExportDocument.MediaData>(StringComparer.Ordinal);
            var inhalte = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var fehlend = new List<string>();
            var laufend = 0;

            string? Anmelden(string dateiname)
            {
                if (medien.TryGetValue(dateiname, out var schon))
                    return schon.Schluessel;

                var bytes = tresor.Read(MediaRoot.Resources, null, dateiname);

                if (bytes == null)
                {
                    fehlend.Add(dateiname);
                    return null;
                }

                var endung = Path.GetExtension(dateiname);
                var schluessel = $"m{++laufend:000}";

                var eintrag = new GameExportDocument.MediaData
                {
                    Schluessel = schluessel,
                    DateiImBuendel = $"{GameExportSerializer.MediaFolderName}/{schluessel}{endung}",
                    Endung = endung,
                    UrsprungsName = dateiname,
                };

                medien[dateiname] = eintrag;
                inhalte[schluessel] = bytes;

                return schluessel;
            }

            var dokument = GameExportMapper.ToDocument(spiel, fragen, kategorien, design, Anmelden);

            if (design != null)
                TexturenAnmelden(design, dokument, tresor, medien, inhalte, ref laufend);

            dokument.Medien.AddRange(medien.Values);

            // Auf einen Hintergrundfaden: das Packen ist der lange Teil des Exports - jede
            // Mediendatei geht durch die Zip-Kompression, und ein Buendel mit Bildern wird
            // schnell zweistellig in Megabyte. Alles darueber laeuft ueber await und gibt die
            // Oberflaeche frei; dieser Aufruf lief bis 2026-09-07 im Fortsetzungszusammenhang
            // des Aufrufers, also auf dem Oberflaechenfaden - das Fenster stand solange.
            await Task.Run(() => Schreibe(zieldatei, dokument, inhalte));

            return new Ergebnis(fragen.Count, inhalte.Count, fehlend, new FileInfo(zieldatei).Length);
        }

        /// <summary>
        /// Nimmt die eigenen Texturen eines Designs mit - nur die eigenen. Was aus dem
        /// Auslieferungsstand kommt, hat der Empfänger ohnehin.
        /// </summary>
        private static void TexturenAnmelden(
            GameTheme design,
            GameExportDocument dokument,
            IMediaVault tresor,
            Dictionary<string, GameExportDocument.MediaData> medien,
            Dictionary<string, byte[]> inhalte,
            ref int laufend)
        {
            if (dokument.Design == null)
                return;

            foreach (var textur in DataModels.Themes.ThemeAssets.Texturen)
            {
                var bytes = tresor.Read(MediaRoot.Theme, design.FolderName, textur.Dateiname);

                if (bytes == null)
                    continue;

                var schluessel = $"t{++laufend:000}";

                medien[$"theme::{textur.Dateiname}"] = new GameExportDocument.MediaData
                {
                    Schluessel = schluessel,
                    DateiImBuendel = $"{GameExportSerializer.MediaFolderName}/{schluessel}{Path.GetExtension(textur.Dateiname)}",
                    Endung = Path.GetExtension(textur.Dateiname),
                    UrsprungsName = textur.Dateiname,
                };

                inhalte[schluessel] = bytes;

                dokument.Design.Texturen[textur.Dateiname] = schluessel;
            }
        }

        private static void Schreibe(
            string zieldatei, GameExportDocument dokument, Dictionary<string, byte[]> inhalte)
        {
            var ordner = Path.GetDirectoryName(zieldatei);

            if (!string.IsNullOrWhiteSpace(ordner))
                Directory.CreateDirectory(ordner);

            using var strom = File.Create(zieldatei);
            using var buendel = new ZipArchive(strom, ZipArchiveMode.Create);

            var eintrag = buendel.CreateEntry(GameExportSerializer.DocumentEntryName);

            using (var offen = eintrag.Open())
                GameExportSerializer.Write(offen, dokument);

            foreach (var medium in dokument.Medien)
            {
                if (!inhalte.TryGetValue(medium.Schluessel, out var bytes))
                    continue;

                var datei = buendel.CreateEntry(medium.DateiImBuendel);

                using var offen = datei.Open();

                offen.Write(bytes, 0, bytes.Length);
            }
        }
    }
}
