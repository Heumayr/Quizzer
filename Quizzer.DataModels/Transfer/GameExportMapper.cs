using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;

namespace Quizzer.DataModels.Transfer
{
    /// <summary>
    /// Übersetzt zwischen den Modellen und dem Bündel-Dokument - in beide Richtungen, und ohne
    /// jeden Datei- oder Datenbankzugriff.
    /// <para>
    /// <b>Hier fällt die Verlaufsfilterung an.</b> Was zum gespielten Abend gehört, geht nicht
    /// mit: Punktestände, gespielte Zellen, laufende Runde, Phase, wer auswählt, Ergebnisse -
    /// und keine einzige Id.
    /// </para>
    /// </summary>
    public static class GameExportMapper
    {
        /// <summary>Wie ein Medium im Bündel angemeldet wird.</summary>
        /// <param name="Schluessel">Der Verweis im Dokument.</param>
        /// <param name="Endung">Die Endung, mit der die Datei beim Import entsteht.</param>
        public delegate string MedienAnmeldung(string dateiname);

        /// <summary>
        /// Baut das Dokument aus den Modellen.
        /// </summary>
        /// <param name="spiel">Das Spiel samt geladenen Zellen und Kopfzeilen.</param>
        /// <param name="fragen">Die Fragen der belegten Zellen, jeweils samt Schritten.</param>
        /// <param name="kategorien">Die Kategorien dieser Fragen.</param>
        /// <param name="design">Das Design des Spiels, oder <c>null</c>.</param>
        /// <param name="medium">
        /// Meldet eine Mediendatei im Bündel an und gibt den Schlüssel zurück. Gibt sie
        /// <c>null</c> zurück, gilt das Medium als nicht vorhanden und der Schritt verliert es.
        /// </param>
        public static GameExportDocument ToDocument(
            Game spiel,
            IReadOnlyList<QuestionBase> fragen,
            IReadOnlyList<Category> kategorien,
            GameTheme? design,
            Func<string, string?> medium)
        {
            var dokument = new GameExportDocument
            {
                Spiel = new GameExportDocument.GameData
                {
                    Designation = spiel.Designation,
                    Height = spiel.Height,
                    Width = spiel.Width,
                    Depth = spiel.Depth,
                    CellHeight = spiel.CellHeight,
                    CellWidth = spiel.CellWidth,
                    DifficultyMultiplier = spiel.DifficultyMultiplier,
                    DifficultyAddition = spiel.DifficultyAddition,
                    DifficultyMinusMultiplier = spiel.DifficultyMinusMultiplier,
                    DifficultyMinusAddition = spiel.DifficultyMinusAddition,
                    PhaseMultiplier = spiel.PhaseMultiplier,
                    PhaseAddition = spiel.PhaseAddition,
                    SuggestedPhases = spiel.SuggestedPhases,
                },
            };

            var kategorieIndex = new Dictionary<Guid, int>();

            foreach (var kategorie in kategorien)
            {
                kategorieIndex[kategorie.Id] = dokument.Kategorien.Count;

                dokument.Kategorien.Add(new GameExportDocument.CategoryData
                {
                    Designation = kategorie.Designation,
                });
            }

            var frageIndex = new Dictionary<Guid, int>();

            foreach (var frage in fragen)
            {
                frageIndex[frage.Id] = dokument.Fragen.Count;

                dokument.Fragen.Add(FrageZuDaten(frage, kategorieIndex, medium));
            }

            foreach (var kopf in spiel.Headers.OrderBy(h => h.HeaderType).ThenBy(h => h.Index))
            {
                dokument.Kopfzeilen.Add(new GameExportDocument.HeaderData
                {
                    Designation = kopf.Designation,
                    HeaderType = kopf.HeaderType,
                    Index = kopf.Index,
                });
            }

            foreach (var zelle in spiel.GameGridCoordinates.OrderBy(c => c.Y).ThenBy(c => c.X).ThenBy(c => c.Z))
            {
                dokument.Zellen.Add(new GameExportDocument.CoordinateData
                {
                    X = zelle.X,
                    Y = zelle.Y,
                    Z = zelle.Z,

                    // Bewusst kein IsDone, keine Phase, keine Punkte - das ist der Abend, nicht
                    // das Spiel.
                    FrageIndex = zelle.QuestionBaseId is Guid id && frageIndex.TryGetValue(id, out var i)
                        ? i
                        : null,
                });
            }

            if (design != null)
                dokument.Design = DesignZuDaten(design, medium);

            return dokument;
        }

        private static GameExportDocument.QuestionData FrageZuDaten(
            QuestionBase frage,
            Dictionary<Guid, int> kategorieIndex,
            Func<string, string?> medium)
        {
            var daten = new GameExportDocument.QuestionData
            {
                Typ = frage.Typ,
                Designation = frage.Designation,
                DesignationShort = frage.DesignationShort,
                QuestionText = frage.QuestionText,
                Notes = frage.Notes,
                KategorieIndex = kategorieIndex.TryGetValue(frage.CategoryId, out var k) ? k : 0,
                Points = frage.Points,
                MinusPoints = frage.MinusPoints,
                Difficulty = frage.Difficulty,
                UseProportionalScoreReductionOnStep = frage.UseProportionalScoreReductionOnStep,
                WarnOnResultStep = frage.WarnOnResultStep,
                WarnOnFinishStep = frage.WarnOnFinishStep,
                UseRandomSequenceOnNoneFinishSteps = frage.UseRandomSequenceOnNoneFinishSteps,
                QuestionViewKeyType = frage.QuestionViewKeyType,
                BuzzerControlsLayout = frage.BuzzerControlsLayout,
                BuzzerMaxAllowedKeySelect = frage.BuzzerMaxAllowedKeySelect,
                ShowTextOnKeySelect = frage.ShowTextOnKeySelect,
                StepDisplayLayoutMode = frage.StepDisplayLayoutMode,
            };

            if (frage is AppreciateQestion schaetz)
            {
                daten.Typeigen = new GameExportDocument.TypeSpecificData
                {
                    ValueKind = schaetz.ValueKind,
                    Unit = schaetz.Unit,
                    ExpectedValue = schaetz.ExpectedValue,
                    ExpectedDate = schaetz.ExpectedDate,
                };
            }

            if (frage is RevealQuestion aufdeck)
            {
                daten.Typeigen = new GameExportDocument.TypeSpecificData
                {
                    Mode = aufdeck.Mode,
                    AreasJson = aufdeck.AreasJson,
                    BlurStart = aufdeck.BlurStart,

                    // Das Bild geht ueber dieselbe Medienliste wie ein Schritt-Medium.
                    BildSchluessel = string.IsNullOrWhiteSpace(aufdeck.ImageFileName)
                        ? null
                        : medium(aufdeck.ImageFileName),
                };
            }

            foreach (var schritt in frage.Steps.OrderBy(s => s.SequenceNumber))
            {
                daten.Schritte.Add(new GameExportDocument.StepData
                {
                    SequenceNumber = schritt.SequenceNumber,
                    Designation = schritt.Designation,
                    StepText = schritt.StepText,
                    IsStart = schritt.IsStart,
                    IsResult = schritt.IsResult,
                    IsFinish = schritt.IsFinish,
                    ResourceTyp = schritt.ResourceTyp,
                    MedienSchluessel = string.IsNullOrWhiteSpace(schritt.ResourceFileName)
                        ? null
                        : medium(schritt.ResourceFileName),
                });
            }

            return daten;
        }

        private static GameExportDocument.ThemeData DesignZuDaten(
            GameTheme design, Func<string, string?> medium)
        {
            var daten = new GameExportDocument.ThemeData
            {
                Designation = design.Designation,
                Notes = design.Notes,
                BackgroundColor = design.BackgroundColor,
                ForegroundColor = design.ForegroundColor,
                CellTextColor = design.CellTextColor,
                HeaderTextColor = design.HeaderTextColor,
            };

            return daten;
        }

        /// <summary>
        /// Baut aus dem Dokument die Modelle - ohne Ids zu vergeben und ohne etwas zu schreiben.
        /// Das tut der Importeur.
        /// </summary>
        /// <param name="dokument">Das geprüfte Dokument.</param>
        /// <param name="medienDatei">
        /// Liefert zu einem Medienschlüssel den Dateinamen, unter dem die Datei auf dem
        /// Zielsystem abgelegt wurde. <c>null</c> heißt: es gibt sie nicht, der Schritt bleibt
        /// ohne Medium.
        /// </param>
        public static ImportBausatz ToEntities(
            GameExportDocument dokument, Func<string, string?> medienDatei)
        {
            GameExportSerializer.Pruefe(dokument);

            var spiel = new Game
            {
                Designation = dokument.Spiel.Designation,
                Height = dokument.Spiel.Height,
                Width = dokument.Spiel.Width,
                Depth = dokument.Spiel.Depth,
                CellHeight = dokument.Spiel.CellHeight,
                CellWidth = dokument.Spiel.CellWidth,
                DifficultyMultiplier = dokument.Spiel.DifficultyMultiplier,
                DifficultyAddition = dokument.Spiel.DifficultyAddition,
                DifficultyMinusMultiplier = dokument.Spiel.DifficultyMinusMultiplier,
                DifficultyMinusAddition = dokument.Spiel.DifficultyMinusAddition,
                PhaseMultiplier = dokument.Spiel.PhaseMultiplier,
                PhaseAddition = dokument.Spiel.PhaseAddition,
                SuggestedPhases = dokument.Spiel.SuggestedPhases,

                // Ein importiertes Spiel beginnt am Anfang, nicht dort, wo das andere aufhoerte.
                State = GameState.Building,
                Phase = 1,
                CurrentRound = 1,
            };

            var fragen = dokument.Fragen.Select(f => DatenZuFrage(f, medienDatei)).ToList();

            var kopfzeilen = dokument.Kopfzeilen.Select(k => new Header
            {
                Designation = k.Designation,
                HeaderType = k.HeaderType,
                Index = k.Index,
            }).ToList();

            var zellen = dokument.Zellen.Select(z => (
                Zelle: new GameGridCoordinate
                {
                    X = z.X,
                    Y = z.Y,
                    Z = z.Z,
                    Phase = 1,
                    IsDone = false,
                },
                FrageIndex: z.FrageIndex)).ToList();

            var design = dokument.Design == null ? null : new GameTheme
            {
                Designation = dokument.Design.Designation,
                Notes = dokument.Design.Notes,
                BackgroundColor = dokument.Design.BackgroundColor,
                ForegroundColor = dokument.Design.ForegroundColor,
                CellTextColor = dokument.Design.CellTextColor,
                HeaderTextColor = dokument.Design.HeaderTextColor,
            };

            return new ImportBausatz(
                spiel,
                dokument.Kategorien.Select(k => k.Designation).ToList(),
                fragen,
                dokument.Fragen.Select(f => f.KategorieIndex).ToList(),
                kopfzeilen,
                zellen,
                design);
        }

        private static QuestionBase DatenZuFrage(
            GameExportDocument.QuestionData daten, Func<string, string?> medienDatei)
        {
            // Der Typ kommt aus der Fabrik, nicht aus einer Zuweisung: QuestionBase.Typ ist
            // nur lesbar, und der Konstruktor des Untertyps setzt ihn samt seinem Profil.
            var frage = Factory.CreateNewQuestion(daten.Typ);

            frage.Designation = daten.Designation;
            frage.DesignationShort = daten.DesignationShort;
            frage.QuestionText = daten.QuestionText;
            frage.Notes = daten.Notes;
            frage.Points = daten.Points;
            frage.MinusPoints = daten.MinusPoints;
            frage.Difficulty = daten.Difficulty;
            frage.UseProportionalScoreReductionOnStep = daten.UseProportionalScoreReductionOnStep;
            frage.WarnOnResultStep = daten.WarnOnResultStep;
            frage.WarnOnFinishStep = daten.WarnOnFinishStep;
            frage.UseRandomSequenceOnNoneFinishSteps = daten.UseRandomSequenceOnNoneFinishSteps;
            frage.QuestionViewKeyType = daten.QuestionViewKeyType;
            frage.BuzzerControlsLayout = daten.BuzzerControlsLayout;
            frage.BuzzerMaxAllowedKeySelect = daten.BuzzerMaxAllowedKeySelect;
            frage.ShowTextOnKeySelect = daten.ShowTextOnKeySelect;
            frage.StepDisplayLayoutMode = daten.StepDisplayLayoutMode;

            if (frage is AppreciateQestion schaetz && daten.Typeigen != null)
            {
                schaetz.ValueKind = daten.Typeigen.ValueKind ?? schaetz.ValueKind;
                schaetz.Unit = daten.Typeigen.Unit ?? schaetz.Unit;
                schaetz.ExpectedValue = daten.Typeigen.ExpectedValue ?? schaetz.ExpectedValue;
                schaetz.ExpectedDate = daten.Typeigen.ExpectedDate;
            }

            if (frage is RevealQuestion aufdeck && daten.Typeigen != null)
            {
                aufdeck.Mode = daten.Typeigen.Mode ?? aufdeck.Mode;
                aufdeck.AreasJson = daten.Typeigen.AreasJson ?? aufdeck.AreasJson;
                aufdeck.BlurStart = daten.Typeigen.BlurStart ?? aufdeck.BlurStart;

                aufdeck.ImageFileName = daten.Typeigen.BildSchluessel == null
                    ? string.Empty
                    : medienDatei(daten.Typeigen.BildSchluessel) ?? string.Empty;
            }

            foreach (var schritt in daten.Schritte.OrderBy(s => s.SequenceNumber))
            {
                frage.Steps.Add(new QuestionStepResource
                {
                    SequenceNumber = schritt.SequenceNumber,
                    Designation = schritt.Designation,
                    StepText = schritt.StepText,
                    IsStart = schritt.IsStart,
                    IsResult = schritt.IsResult,
                    IsFinish = schritt.IsFinish,
                    ResourceTyp = schritt.ResourceTyp,
                    ResourceFileName = schritt.MedienSchluessel == null
                        ? string.Empty
                        : medienDatei(schritt.MedienSchluessel) ?? string.Empty,
                });
            }

            return frage;
        }

        /// <summary>
        /// Was aus einem Dokument entsteht, bevor der Importeur es anlegt. Bewusst ohne Ids und
        /// ohne Verknüpfungen - die entstehen erst beim Schreiben.
        /// </summary>
        /// <param name="Spiel">Das Spiel selbst.</param>
        /// <param name="Kategorien">Die Bezeichnungen der Kategorien, in der Reihenfolge des Dokuments.</param>
        /// <param name="Fragen">Die Fragen, in der Reihenfolge des Dokuments.</param>
        /// <param name="KategorieJeFrage">Je Frage der Index ihrer Kategorie.</param>
        /// <param name="Kopfzeilen">Die Kopfzeilen des Rasters.</param>
        /// <param name="Zellen">Je Zelle die Koordinate und der Index ihrer Frage.</param>
        /// <param name="Design">Das Design, oder <c>null</c>.</param>
        public sealed record ImportBausatz(
            Game Spiel,
            List<string> Kategorien,
            List<QuestionBase> Fragen,
            List<int> KategorieJeFrage,
            List<Header> Kopfzeilen,
            List<(GameGridCoordinate Zelle, int? FrageIndex)> Zellen,
            GameTheme? Design);
    }
}
