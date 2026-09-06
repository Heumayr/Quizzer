using Quizzer.DataModels;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Transfer;
using Quizzer.Logic.Controller.TypedControllers;
using System.IO.Compression;

namespace Quizzer.Logic.Transfer
{
    /// <summary>
    /// Liest ein Bündel und legt daraus ein Spiel an - neben dem Bestand, nie darüber.
    /// <para>
    /// <b>Nutzerentscheidungen vom 2026-09-06:</b> der Spielleiter ist der <b>angemeldete</b>,
    /// die Mitspieler werden beim Import gewählt (oder keine), und Fragen werden <b>immer neu
    /// angelegt</b> (F09). Der letzte Punkt ist der wichtige: würde eine gleichnamige Frage
    /// stattdessen überschrieben, löschte <c>SaveWithStepsAsync</c> dabei jeden Schritt, der
    /// nicht im Bündel steht - und das ließe sich nicht zurückdrehen.
    /// </para>
    /// <para>
    /// <b>Kategorien werden zusammengeführt</b>, denn eine Kategorie hat außer ihrer Bezeichnung
    /// keine Nutzlast; da kann nichts verlorengehen.
    /// </para>
    /// <para>
    /// <b>Alles in einer Klammer.</b> Alle Controller teilen sich über
    /// <c>ControllerBase(other)</c> einen Kontext, und geschrieben wird genau einmal - ein
    /// Fehler mittendrin lässt kein halbes Spiel stehen.
    /// </para>
    /// </summary>
    public static class GameImporter
    {
        /// <summary>Was der Import angelegt hat.</summary>
        /// <param name="Spiel">Das neue Spiel.</param>
        /// <param name="Fragen">Anzahl neu angelegter Fragen.</param>
        /// <param name="NeueKategorien">Kategorien, die es noch nicht gab.</param>
        /// <param name="Medien">Anzahl abgelegter Mediendateien.</param>
        public sealed record Ergebnis(Game Spiel, int Fragen, int NeueKategorien, int Medien);

        /// <summary>
        /// Liest das Bündel und legt das Spiel an.
        /// </summary>
        /// <param name="quelldatei">Die Bündeldatei.</param>
        /// <param name="tresor">Wohin die Medien gelegt werden.</param>
        /// <param name="moderatorId">
        /// Der Spielleiter des neuen Spiels - im Regelfall der angemeldete. <c>null</c> lässt
        /// ihn offen; das Spiel ist dann erst nach dem Eintragen startbar.
        /// </param>
        /// <param name="mitspielerIds">Die gewählten Mitspieler; leer heißt: keine.</param>
        public static async Task<Ergebnis> ImportAsync(
            string quelldatei,
            IMediaVault tresor,
            Guid? moderatorId,
            IReadOnlyList<Guid> mitspielerIds)
        {
            var (dokument, dateien) = Lies(quelldatei);

            // Erst die Medien ablegen - der Tresor vergibt die Namen, nicht das Buendel.
            var medienNamen = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var medium in dokument.Medien)
            {
                if (!dateien.TryGetValue(medium.Schluessel, out var bytes))
                    continue;

                medienNamen[medium.Schluessel] =
                    tresor.Write(MediaRoot.Resources, null, medium.Endung, bytes);
            }

            var bausatz = GameExportMapper.ToEntities(
                dokument, s => medienNamen.TryGetValue(s, out var n) ? n : null);

            return await SchreibeAsync(bausatz, dokument, moderatorId, mitspielerIds, medienNamen.Count);
        }

        private static (GameExportDocument Dokument, Dictionary<string, byte[]> Dateien) Lies(string quelldatei)
        {
            using var strom = File.OpenRead(quelldatei);
            using var buendel = new ZipArchive(strom, ZipArchiveMode.Read);

            var doku = buendel.GetEntry(GameExportSerializer.DocumentEntryName)
                ?? throw new InvalidDataException(
                    $"Im Buendel fehlt '{GameExportSerializer.DocumentEntryName}'. "
                    + "Vermutlich ist es keine Quizzer-Datei.");

            GameExportDocument dokument;

            using (var offen = doku.Open())
            {
                // Der Eintrag eines Zip-Archivs laesst sich nicht zurueckspulen - erst in den
                // Speicher, dann lesen.
                using var puffer = new MemoryStream();

                offen.CopyTo(puffer);
                puffer.Position = 0;

                dokument = GameExportSerializer.Read(puffer);
            }

            var dateien = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            foreach (var medium in dokument.Medien)
            {
                var eintrag = buendel.GetEntry(medium.DateiImBuendel);

                if (eintrag == null)
                    continue;

                using var offen = eintrag.Open();
                using var puffer = new MemoryStream();

                offen.CopyTo(puffer);

                dateien[medium.Schluessel] = puffer.ToArray();
            }

            return (dokument, dateien);
        }

        private static async Task<Ergebnis> SchreibeAsync(
            GameExportMapper.ImportBausatz bausatz,
            GameExportDocument dokument,
            Guid? moderatorId,
            IReadOnlyList<Guid> mitspielerIds,
            int medienAnzahl)
        {
            using var ctrlSpiele = new GamesController();
            using var ctrlKategorien = new CategoriesController(ctrlSpiele);
            using var ctrlFragen = new QuestionBasesController(ctrlSpiele);
            using var ctrlKopf = new HeadersController(ctrlSpiele);
            using var ctrlZellen = new GameGridCoordinatesController(ctrlSpiele);
            using var ctrlLink = new PlayerXGamesController(ctrlSpiele);
            using var ctrlDesign = new GameThemesController(ctrlSpiele);

            // Kategorien zusammenfuehren - ueber die Bezeichnung, denn mehr traegt eine nicht.
            var vorhandene = (await ctrlKategorien.GetAllAsync())
                .GroupBy(k => k.Designation, StringComparer.CurrentCultureIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.CurrentCultureIgnoreCase);

            var kategorieIds = new List<Guid>();
            var neueKategorien = 0;

            foreach (var name in bausatz.Kategorien)
            {
                if (vorhandene.TryGetValue(name, out var da))
                {
                    kategorieIds.Add(da);
                    continue;
                }

                var neu = new Category { Id = Guid.NewGuid(), Designation = name };

                await ctrlKategorien.InsertAsync(neu);

                vorhandene[name] = neu.Id;
                kategorieIds.Add(neu.Id);
                neueKategorien++;
            }

            // Das Design: nur anlegen, wenn es eines gibt und noch keines gleichen Namens.
            Guid? designId = null;

            if (bausatz.Design != null)
            {
                var vorhandenesDesign = (await ctrlDesign.GetAllAsync())
                    .FirstOrDefault(d => string.Equals(
                        d.Designation, bausatz.Design.Designation, StringComparison.CurrentCultureIgnoreCase));

                if (vorhandenesDesign != null)
                {
                    designId = vorhandenesDesign.Id;
                }
                else
                {
                    bausatz.Design.Id = Guid.NewGuid();
                    bausatz.Design.FolderName = OrdnerName(bausatz.Design.Designation);

                    await ctrlDesign.InsertAsync(bausatz.Design);

                    designId = bausatz.Design.Id;
                }
            }

            // Die Fragen - IMMER neu angelegt (F09).
            for (var i = 0; i < bausatz.Fragen.Count; i++)
            {
                var frage = bausatz.Fragen[i];

                frage.Id = Guid.NewGuid();
                frage.CategoryId = kategorieIds[bausatz.KategorieJeFrage[i]];
                frage.OwnerPlayerId = moderatorId;

                foreach (var schritt in frage.Steps)
                {
                    schritt.Id = Guid.NewGuid();
                    schritt.QuestionBaseId = frage.Id;
                }

                await ctrlFragen.SaveWithStepsAsync(frage);
            }

            // Das Spiel selbst.
            var spiel = bausatz.Spiel;

            spiel.Id = Guid.NewGuid();
            spiel.ModeratorPlayerId = moderatorId;
            spiel.GameThemeId = designId;

            await ctrlSpiele.InsertAsync(spiel);

            foreach (var kopf in bausatz.Kopfzeilen)
            {
                kopf.Id = Guid.NewGuid();
                kopf.GameId = spiel.Id;

                await ctrlKopf.InsertAsync(kopf);
            }

            foreach (var (zelle, frageIndex) in bausatz.Zellen)
            {
                zelle.Id = Guid.NewGuid();
                zelle.GameId = spiel.Id;
                zelle.Game = spiel;

                if (frageIndex is int index)
                {
                    zelle.QuestionBaseId = bausatz.Fragen[index].Id;
                    zelle.QuestionBase = bausatz.Fragen[index];
                }

                // Die Punkte sind gespeicherte Spalten. Ohne diesen Aufruf - und ohne die beiden
                // Rueckverweise darueber - stuende in jeder Zelle eine Null.
                zelle.CalculateAndSetCurrentPoints();

                await ctrlZellen.InsertAsync(zelle);
            }

            foreach (var spielerId in mitspielerIds.Distinct())
            {
                await ctrlLink.InsertAsync(new PlayerXGame
                {
                    Id = Guid.NewGuid(),
                    GameId = spiel.Id,
                    PlayerId = spielerId,
                });
            }

            // Ein einziges Schreiben - alles oder nichts.
            await ctrlSpiele.SaveChangesAsync();

            return new Ergebnis(spiel, bausatz.Fragen.Count, neueKategorien, medienAnzahl);
        }

        /// <summary>Ein Ordnername für ein importiertes Design - aus der Bezeichnung, entschärft.</summary>
        private static string OrdnerName(string bezeichnung)
        {
            var name = bezeichnung;

            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            return string.IsNullOrWhiteSpace(name) ? "Import" : name.Trim();
        }
    }
}
