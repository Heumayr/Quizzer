using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.Logic.Demo
{
    public static partial class DemoDataSeeder
    {
        /// <summary>Was die Typenschau angelegt hat.</summary>
        /// <param name="Spiel">Das angelegte Spiel.</param>
        /// <param name="Fragen">Anzahl neuer Fragen.</param>
        /// <param name="Typen">Welche Fragetypen darin vorkommen.</param>
        /// <param name="OhneBilder">Ob die Bilderfragen fehlen, weil der Ressourcenordner nicht beschreibbar war.</param>
        public sealed record TypenschauErgebnis(
            Game Spiel, int Fragen, IReadOnlyList<QuestionType> Typen, bool OhneBilder);

        /// <summary>
        /// Legt das Spiel „Alle Fragetypen" an: <b>je Fragetyp eine Spalte</b> mit zwei Fragen,
        /// die zusammen jede Besonderheit des Typs einmal zeigen (M12-Nachtrag, 2026-10-21:
        /// „so dass ich alle funktionalitäten leicht und schnell selber prüfen kann").
        /// <para>
        /// Was darin steckt: Bild in einer Standardfrage, Multiple Choice mit Bildantworten und
        /// mit zwei richtigen langen Antworten, Schätzfrage mit Zahl und mit Datum, Aufdeckfrage
        /// mit Flächen und mit Unschärfe, Reihenfolge mit vier Elementen, Zuordnung mit Text und
        /// mit Flaggen als Gegenstück.
        /// </para>
        /// <para>
        /// <b>Neue Typen gehören hier hinein</b> - <c>DemoDataSeederUnitTests</c> prüft, dass
        /// jeder Typ aus <see cref="QuestionTypeProfiles.All"/> vorkommt.
        /// </para>
        /// </summary>
        public static async Task<TypenschauErgebnis> CreateTypenschauAsync()
        {
            var mitBildern = DemoBilder.SchreibeAlle();

            var kategorie = await TypenschauKategorieAsync();
            var spieler = await DemoSpielerAsync();

            var spalten = BaueTypenschau(kategorie.Id, mitBildern)
                .GroupBy(f => f.Typ)
                .OrderBy(g => QuestionTypeProfiles.All.Select(p => p.Typ).ToList().IndexOf(g.Key))
                .Select(g => g.ToList())
                .ToList();

            var leiter = spieler[^1];

            foreach (var frage in spalten.SelectMany(s => s))
            {
                frage.OwnerPlayerId = leiter.Id;
                await SaveQuestionAsync(frage);
            }

            var spiel = await TypenschauSpielAsync(spalten, spieler);

            return new TypenschauErgebnis(
                spiel,
                spalten.Sum(s => s.Count),
                spalten.Select(s => s[0].Typ).ToList(),
                !mitBildern);
        }

        private static async Task<Category> TypenschauKategorieAsync()
        {
            using var ctrl = new CategoriesController();

            var kategorie = new Category
            {
                Id = Guid.NewGuid(),
                Designation = $"{Marke} Alle Fragetypen",
            };

            await ctrl.InsertAsync(kategorie);
            await ctrl.SaveChangesAsync();

            return kategorie;
        }

        /// <summary>
        /// Die Demo-Mitspieler - vorhandene werden wiederverwendet, sonst angelegt. Zwei Läufe
        /// sollen nicht zweimal „Anna" in die Spielerliste schreiben.
        /// </summary>
        private static async Task<List<Player>> DemoSpielerAsync()
        {
            using (var ctrl = new PlayersController())
            {
                var namen = new[] { "Anna", "Bert", "Clara", "Dennis", "Moderator" };

                var vorhanden = (await ctrl.GetAllAsync())
                    .Where(p => p.Designation.StartsWith(Marke, StringComparison.Ordinal))
                    .ToList();

                var gefunden = namen
                    .Select(n => vorhanden.FirstOrDefault(p => p.Designation == $"{Marke} {n}"))
                    .ToList();

                if (gefunden.All(p => p != null))
                    return gefunden!;
            }

            return await CreatePlayersAsync();
        }

        private static IEnumerable<QuestionBase> BaueTypenschau(Guid kat, bool mitBildern)
        {
            // --- Standard ---
            yield return Standard(kat, "Typenschau: Hauptstadt", "STD-1", 100, Difficulty.Level1,
                "Wie heißt die Hauptstadt von Kanada?",
                ["Sie liegt in der Provinz Ontario.", "Es ist nicht Toronto."],
                "Ottawa");

            if (mitBildern)
            {
                var bildfrage = Standard(kat, "Typenschau: Flagge mit Bild", "STD-2", 200, Difficulty.Level2,
                    "Zu welchem Land gehört diese Flagge?",
                    ["Hinweis: Das Land besteht aus über 6000 Inseln."],
                    "Japan");

                MitBild(ErsterNormalerSchritt(bildfrage)!, "japan");
                yield return bildfrage;
            }
            else
            {
                yield return Standard(kat, "Typenschau: Fluss", "STD-2", 200, Difficulty.Level2,
                    "Welcher Fluss fließt durch Wien?", ["Er ist der zweitlängste Fluss Europas."], "Die Donau");
            }

            // --- Multiple Choice ---
            if (mitBildern)
            {
                var bilder = MultipleChoice(kat, "Typenschau: Flaggen als Antworten", "MC-1", 100, Difficulty.Level1,
                    "Welche Flagge gehört zu Italien?",
                    [("Italien", true), ("Frankreich", false), ("Irland", false), ("Belgien", false)],
                    "Grün, Weiß, Rot - Italien.");

                // Nur Bild, kein Text: sonst stünde die Lösung auf der Taste.
                bilder.ShowTextOnKeySelect = false;

                foreach (var schritt in bilder.Steps.Where(s => !s.IsStart && !s.IsFinish))
                {
                    MitBild(schritt, schritt.StepText.ToLowerInvariant());
                    schritt.StepText = string.Empty;
                    schritt.Designation = string.Empty;
                }

                yield return bilder;
            }
            else
            {
                yield return MultipleChoice(kat, "Typenschau: Planet", "MC-1", 100, Difficulty.Level1,
                    "Welcher Planet ist der Sonne am nächsten?",
                    [("Merkur", true), ("Venus", false), ("Mars", false), ("Erde", false)],
                    "Merkur.");
            }

            var lang = MultipleChoice(kat, "Typenschau: Zwei richtige, lange Antworten", "MC-2", 200, Difficulty.Level2,
                "Welche beiden Aussagen über den Mond stimmen?",
                [
                    ("Der Mond zeigt der Erde immer ungefähr dieselbe Seite, weil er sich in derselben Zeit einmal um sich selbst dreht, in der er die Erde umrundet.", true),
                    ("Auf der Rückseite des Mondes ist es immer dunkel, weil dort nie die Sonne scheint.", false),
                    ("Die Gezeiten der Meere entstehen zum größten Teil durch die Anziehungskraft des Mondes.", true),
                    ("Der Mond ist ungefähr halb so groß wie die Erde.", false),
                ],
                "Gebundene Rotation und Gezeiten stimmen. Die Rückseite bekommt genauso Sonne, und der Mond hat etwa ein Viertel des Erddurchmessers.");

            // Der volle Text auch am Telefon - genau daran soll sich die Lesbarkeit zeigen.
            // Schritt() kuerzt die Bezeichnung sonst auf 40 Zeichen.
            foreach (var schritt in lang.Steps.Where(s => !s.IsFinish))
                schritt.Designation = schritt.StepText;

            yield return lang;

            // --- Eigenschaften ---
            yield return Eigenschaften(kat, "Typenschau: Welches Tier?", "EIG-1", 100, Difficulty.Level1,
                "Welches Tier wird gesucht?",
                ["Es ist das größte Landtier.", "Es hat einen Rüssel.", "Es lebt in Afrika und Asien."],
                "Der Elefant");

            yield return Eigenschaften(kat, "Typenschau: Welche Stadt?", "EIG-2", 200, Difficulty.Level2,
                "Welche Stadt wird gesucht?",
                ["Sie liegt an der Donau.", "Dort steht der Stephansdom.", "Sie ist die Hauptstadt Österreichs."],
                "Wien");

            // --- Schaetzfrage ---
            yield return Schaetzfrage(kat, "Typenschau: Zahl schätzen", "SCH-1", 100, Difficulty.Level1,
                "Wie viele Einwohner hat Österreich (in Millionen, eine Nachkommastelle)?", 9.2,
                AppreciateValueKind.Number, AppreciateUnit.Stueck, "Rund 9,2 Millionen");

            yield return Schaetzfrage(kat, "Typenschau: Datum schätzen", "SCH-2", 200, Difficulty.Level2,
                "Wann fiel die Berliner Mauer?", new DateTime(1989, 11, 9), "9. November 1989");

            // --- Aufdeckfrage ---
            if (mitBildern)
            {
                yield return Aufdeckfrage(kat, "Typenschau: Flächen aufdecken", "AUF-1", 100, Difficulty.Level1,
                    "Welche Flagge wird aufgedeckt?", "deutschland", RevealMode.Areas, "Deutschland");

                yield return Aufdeckfrage(kat, "Typenschau: Unschärfe", "AUF-2", 200, Difficulty.Level2,
                    "Welche Flagge wird schärfer?", "schweiz", RevealMode.Blur, "Die Schweiz");
            }

            // --- Reihenfolge ---
            yield return Reihenfolge(kat, "Typenschau: Planeten ordnen", "REI-1", 100, Difficulty.Level1,
                "Ordne die Planeten nach ihrem Abstand zur Sonne - der nächste zuerst.",
                ["Merkur", "Venus", "Erde", "Mars"]);

            yield return Reihenfolge(kat, "Typenschau: Erfindungen ordnen", "REI-2", 200, Difficulty.Level2,
                "Ordne die Erfindungen nach ihrem Jahr - die älteste zuerst.",
                ["Telefon (1876)", "Glühbirne (1879)", "Automobil (1886)", "Motorflugzeug (1903)"]);

            // --- Zuordnung ---
            yield return Zuordnung(kat, "Typenschau: Hauptstädte zuordnen", "ZUO-1", 100, Difficulty.Level1,
                "Ordne jedem Land seine Hauptstadt zu.",
                [("Österreich", "Wien"), ("Schweiz", "Bern"), ("Deutschland", "Berlin"), ("Frankreich", "Paris")]);

            if (mitBildern)
            {
                var flaggen = Zuordnung(kat, "Typenschau: Flaggen zuordnen", "ZUO-2", 200, Difficulty.Level2,
                    "Ordne jedem Land seine Flagge zu.",
                    [("Italien", "italien"), ("Irland", "irland"), ("Belgien", "belgien"), ("Japan", "japan")]);

                // Gegenstueck nur als Bild - der Text waere die Loesung.
                flaggen.ShowTextOnKeySelect = false;

                foreach (var paar in flaggen.Steps)
                {
                    MitBild(paar, paar.StepText);
                    paar.StepText = string.Empty;
                    paar.Designation = string.Empty;
                }

                yield return flaggen;
            }
            else
            {
                yield return Zuordnung(kat, "Typenschau: Erfinder zuordnen", "ZUO-2", 200, Difficulty.Level2,
                    "Ordne jeder Erfindung ihren Erfinder zu.",
                    [("Telefon", "Bell"), ("Glühbirne", "Edison"), ("Buchdruck", "Gutenberg")]);
            }
        }

        /// <summary>Zuordnung: je Paar ein Schritt - links der Begriff, rechts das Gegenstück.</summary>
        private static QuestionBase Zuordnung(
            Guid kategorieId, string bezeichnung, string kurz, int punkte, Difficulty stufe,
            string frageText, (string Links, string Rechts)[] paare)
        {
            var frage = new MatchingQuestion();
            Grunddaten(frage, kategorieId, bezeichnung, kurz, punkte, stufe);
            frage.MinusPoints = 0;
            frage.QuestionText = frageText;
            frage.ShowTextOnKeySelect = true;

            for (var i = 0; i < paare.Length; i++)
            {
                var schritt = Schritt(frage.Id, (i + 1) * 10, paare[i].Rechts);
                schritt.PartnerText = paare[i].Links;
                schritt.SolutionNumber = i + 1;
                frage.Steps.Add(schritt);
            }

            frage.BuzzerMaxAllowedKeySelect = paare.Length;

            return frage;
        }

        private static void MitBild(QuestionStepResource schritt, string flagge)
        {
            schritt.ResourceFileName = DemoBilder.Dateiname(flagge);
            schritt.ResourceTyp = ResourceType.Image;
        }

        /// <summary>
        /// Aufdeckfrage. Bei Flächen deckt Schritt n die Fläche n auf (ein Raster aus 3 × 2);
        /// bei Unschärfe wird das Bild über fünf Schritte scharf.
        /// </summary>
        private static QuestionBase Aufdeckfrage(
            Guid kategorieId, string bezeichnung, string kurz, int punkte, Difficulty stufe,
            string frageText, string flagge, RevealMode modus, string loesung)
        {
            var frage = new RevealQuestion();
            Grunddaten(frage, kategorieId, bezeichnung, kurz, punkte, stufe);
            frage.QuestionText = frageText;
            frage.Mode = modus;
            frage.ImageFileName = DemoBilder.Dateiname(flagge);

            int schritte;

            if (modus == RevealMode.Areas)
            {
                var flaechen = new List<RevealArea>();

                for (var zeile = 0; zeile < 2; zeile++)
                    for (var spalte = 0; spalte < 3; spalte++)
                        flaechen.Add(new RevealArea(spalte / 3.0, zeile / 2.0, 1 / 3.0, 0.5));

                frage.AreasJson = RevealAreas.ToJson(flaechen);
                schritte = RevealAreas.Schrittzahl(flaechen);
            }
            else
            {
                schritte = 5;
            }

            for (var i = 0; i < schritte; i++)
            {
                var schritt = Schritt(frage.Id, (i + 1) * 10, string.Empty);
                schritt.Designation = $"Aufdecken {i + 1}";
                frage.Steps.Add(schritt);
            }

            frage.Steps.Add(Schritt(frage.Id, 900, loesung, istAbschluss: true));

            return frage;
        }

        /// <summary>Reihenfolge: die Elemente in der richtigen Folge, wie die Maske sie stempelt.</summary>
        private static QuestionBase Reihenfolge(
            Guid kategorieId, string bezeichnung, string kurz, int punkte, Difficulty stufe,
            string frageText, string[] elementeInReihenfolge)
        {
            var frage = new OrderingQuestion();
            Grunddaten(frage, kategorieId, bezeichnung, kurz, punkte, stufe);
            frage.MinusPoints = 0;
            frage.QuestionText = frageText;
            frage.ShowTextOnKeySelect = true;

            for (var i = 0; i < elementeInReihenfolge.Length; i++)
            {
                var schritt = Schritt(frage.Id, (i + 1) * 10, elementeInReihenfolge[i]);
                schritt.SolutionNumber = i + 1;
                frage.Steps.Add(schritt);
            }

            frage.BuzzerMaxAllowedKeySelect = elementeInReihenfolge.Length;

            return frage;
        }

        /// <summary>Das Raster: je Typ eine Spalte, zwei Zeilen (100 und 200 Punkte).</summary>
        private static async Task<Game> TypenschauSpielAsync(
            List<List<QuestionBase>> spalten, List<Player> spieler)
        {
            var moderator = spieler[^1];
            var mitspieler = spieler[..^1];
            var zeilen = spalten.Max(s => s.Count);

            using var spieleCtrl = new GamesController();

            var nummer = (await spieleCtrl.GetAllAsync())
                .Count(g => g.Designation.StartsWith($"{Marke} Alle Fragetypen", StringComparison.Ordinal)) + 1;

            var spiel = new Game
            {
                Id = Guid.NewGuid(),
                Designation = $"{Marke} Alle Fragetypen {nummer}",
                Width = spalten.Count,
                Height = zeilen,
                CellWidth = 200,
                CellHeight = 110,
                Phase = 1,
                CurrentRound = 0,
                SuggestedPhases = 1,
                State = GameState.Building,
                ModeratorPlayerId = moderator.Id,
            };

            await spieleCtrl.UpsertAsync(spiel);
            await spieleCtrl.SaveChangesAsync();

            using var ctrlZellen = new GameGridCoordinatesController();
            using var ctrlKopf = new HeadersController(ctrlZellen);
            using var ctrlLink = new PlayerXGamesController(ctrlZellen);

            for (var spalte = 0; spalte < spalten.Count; spalte++)
            {
                await ctrlKopf.InsertAsync(new Header
                {
                    Id = Guid.NewGuid(),
                    GameId = spiel.Id,
                    HeaderType = HeaderType.Column,
                    Index = spalte + 1,
                    Designation = QuestionTypeProfiles.For(spalten[spalte][0].Typ).DisplayName,
                });
            }

            for (var zeile = 0; zeile < zeilen; zeile++)
            {
                await ctrlKopf.InsertAsync(new Header
                {
                    Id = Guid.NewGuid(),
                    GameId = spiel.Id,
                    HeaderType = HeaderType.Row,
                    Index = zeile + 1,
                    Designation = $"{(zeile + 1) * 100}",
                });
            }

            for (var spalte = 0; spalte < spalten.Count; spalte++)
            {
                for (var zeile = 0; zeile < zeilen; zeile++)
                {
                    var frage = spalten[spalte].ElementAtOrDefault(zeile);

                    var zelle = new GameGridCoordinate
                    {
                        Id = Guid.NewGuid(),
                        GameId = spiel.Id,
                        Game = spiel,
                        X = spalte,
                        Y = zeile,
                        Phase = 1,
                        QuestionBaseId = frage?.Id,
                        QuestionBase = frage,
                    };

                    zelle.CalculateAndSetCurrentPoints();
                    spiel.GameGridCoordinates.Add(zelle);

                    await ctrlZellen.InsertAsync(zelle);
                }
            }

            foreach (var p in mitspieler)
            {
                await ctrlLink.InsertAsync(new PlayerXGame
                {
                    Id = Guid.NewGuid(),
                    GameId = spiel.Id,
                    PlayerId = p.Id,
                });
            }

            await ctrlZellen.SaveChangesAsync();

            return spiel;
        }
    }
}
