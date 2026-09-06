using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.Logic.Demo
{
    /// <summary>
    /// Legt einen vollstaendig spielbaren Demo-Quizabend an: vier Kategorien, zwoelf Fragen
    /// ueber alle vier Fragetypen, fuenf Mitspieler und ein fertiges Spielfeld.
    /// <para>
    /// <b>Nur additiv.</b> Alles traegt <see cref="Marke"/> in der Bezeichnung und laesst sich
    /// darueber wieder vollstaendig entfernen. Bestehende Kategorien, Fragen, Mitspieler und
    /// Spiele werden nicht angefasst.
    /// </para>
    /// <para>
    /// Die Fragen erfuellen die Regeln aus <c>QuestionValidator</c> je Typ: Multiple Choice
    /// braucht mindestens zwei Antwortschritte und genau so viele Loesungen, wie
    /// <c>BuzzerMaxAllowedKeySelect</c> zulaesst; eine Schaetzfrage vertraegt genau einen
    /// Loesungsschritt und braucht Sollwert samt passender Einheit.
    /// </para>
    /// </summary>
    public static class DemoDataSeeder
    {
        /// <summary>Woran die Demodaten zu erkennen sind. Steht in jeder Bezeichnung.</summary>
        public const string Marke = "[Demo]";

        /// <summary>Was beim Anlegen entstanden ist.</summary>
        /// <param name="Spiel">Das angelegte Spiel.</param>
        /// <param name="Kategorien">Anzahl neuer Kategorien.</param>
        /// <param name="Fragen">Anzahl neuer Fragen.</param>
        /// <param name="Mitspieler">Anzahl neuer Mitspieler, den Moderator eingerechnet.</param>
        /// <param name="Zellen">Anzahl belegter Spielfeldzellen.</param>
        public sealed record Ergebnis(Game Spiel, int Kategorien, int Fragen, int Mitspieler, int Zellen);

        /// <summary>
        /// Legt den Demo-Quizabend an. Mehrfaches Aufrufen erzeugt weitere, durchnummerierte
        /// Spiele - es wird nichts ueberschrieben.
        /// </summary>
        public static async Task<Ergebnis> CreateAsync()
        {
            var kategorien = await CreateCategoriesAsync();
            var fragen = await CreateQuestionsAsync(kategorien);
            var spieler = await CreatePlayersAsync();
            var spiel = await CreateGameAsync(kategorien, fragen, spieler);

            return new Ergebnis(spiel, kategorien.Count, fragen.Count, spieler.Count,
                spiel.GameGridCoordinates.Count(c => c.QuestionBaseId.HasValue));
        }

        private static async Task<List<Category>> CreateCategoriesAsync()
        {
            var namen = new[] { "Geographie", "Musik", "Film & Fernsehen", "Naturwissenschaft" };
            var ergebnis = new List<Category>();

            using var ctrl = new CategoriesController();

            foreach (var name in namen)
            {
                var kategorie = new Category
                {
                    Id = Guid.NewGuid(),
                    Designation = $"{Marke} {name}",
                };

                await ctrl.InsertAsync(kategorie);
                ergebnis.Add(kategorie);
            }

            await ctrl.SaveChangesAsync();

            return ergebnis;
        }

        private static async Task<List<Player>> CreatePlayersAsync()
        {
            var namen = new[]
            {
                ("Anna", "Anna"),
                ("Bert", "Bert"),
                ("Clara", "Clara"),
                ("Dennis", "Dennis"),
                ("Moderator", "Der Spielleiter"),
            };

            var ergebnis = new List<Player>();

            using var ctrl = new PlayersController();

            foreach (var (kurz, anzeige) in namen)
            {
                var spieler = new Player
                {
                    Id = Guid.NewGuid(),
                    Designation = $"{Marke} {kurz}",
                    DisplayName = anzeige,
                };

                await ctrl.InsertAsync(spieler);
                ergebnis.Add(spieler);
            }

            await ctrl.SaveChangesAsync();

            return ergebnis;
        }

        /// <summary>
        /// Schreibt eine Frage samt ihren Schritten. <c>SaveWithStepsAsync</c> ist der einzige
        /// Weg, der die Schritte mitnimmt - <c>UpsertAsync</c> leert sie im Klon.
        /// </summary>
        private static async Task SaveQuestionAsync(QuestionBase frage)
        {
            using var ctrl = new QuestionBasesController();

            await ctrl.SaveWithStepsAsync(frage);
            await ctrl.SaveChangesAsync();
        }

        private static QuestionStepResource Schritt(
            Guid frageId, int nummer, string text,
            bool istStart = false, bool istLoesung = false, bool istAbschluss = false)
            => new()
            {
                Id = Guid.NewGuid(),
                QuestionBaseId = frageId,
                SequenceNumber = nummer,
                Designation = text.Length <= 40 ? text : text[..40],
                StepText = text,
                IsStart = istStart,
                IsResult = istLoesung,
                IsFinish = istAbschluss,
            };

        private static async Task<List<QuestionBase>> CreateQuestionsAsync(List<Category> kategorien)
        {
            var alle = new List<QuestionBase>();

            foreach (var frage in BaueFragen(kategorien))
            {
                await SaveQuestionAsync(frage);
                alle.Add(frage);
            }

            return alle;
        }

        /// <summary>
        /// Die zwoelf Fragen: je Kategorie drei, aufsteigend schwerer, und ueber alle vier
        /// Fragetypen verteilt, damit sich jeder Typ durchprobieren laesst.
        /// </summary>
        private static IEnumerable<QuestionBase> BaueFragen(List<Category> kategorien)
        {
            var geo = kategorien[0].Id;
            var musik = kategorien[1].Id;
            var film = kategorien[2].Id;
            var natur = kategorien[3].Id;

            yield return Standard(geo, "Hauptstadt von Australien", "AUS", 100, Difficulty.Level1,
                "Welche Stadt ist die Hauptstadt von Australien?",
                ["Sie ist nicht die größte Stadt des Landes.", "Sie wurde eigens als Hauptstadt geplant."],
                "Canberra");

            yield return Schaetzfrage(geo, "Höhe des Großglockners", "GLO", 200, Difficulty.Level2,
                "Wie hoch ist der Großglockner?", 3798, AppreciateValueKind.Length, AppreciateUnit.Meter,
                "3798 Meter");

            yield return Eigenschaften(natur, "Welches Element?", "ELE", 300, Difficulty.Level3,
                "Welches chemische Element wird gesucht?",
                ["Es ist ein Edelgas.", "Es ist das zweithäufigste Element im Universum.",
                 "Ballons steigen damit auf."],
                "Helium");

            yield return MultipleChoice(musik, "Bohemian Rhapsody", "BOH", 100, Difficulty.Level1,
                "Welche Band spielte \"Bohemian Rhapsody\" ein?",
                [("Queen", true), ("The Beatles", false), ("Pink Floyd", false), ("Led Zeppelin", false)],
                "Queen, erschienen 1975.");

            yield return Standard(musik, "Das Instrument", "INS", 200, Difficulty.Level2,
                "Welches Instrument hat schwarze und weiße Tasten und Saiten im Inneren?",
                ["Es steht oft in Konzertsälen.", "Es gibt es als Flügel und als aufrechte Form."],
                "Das Klavier");

            yield return Schaetzfrage(musik, "Tasten eines Klaviers", "TAS", 300, Difficulty.Level3,
                "Wie viele Tasten hat ein modernes Klavier?", 88, AppreciateValueKind.Number,
                AppreciateUnit.Stueck, "88 Tasten");

            yield return Standard(film, "Der weiße Hai", "HAI", 100, Difficulty.Level1,
                "Welcher Regisseur drehte \"Der weiße Hai\"?",
                ["Er drehte später auch \"Jurassic Park\"."],
                "Steven Spielberg");

            yield return MultipleChoice(film, "Herr der Ringe", "HDR", 200, Difficulty.Level2,
                "In welchem Land wurde die \"Herr der Ringe\"-Trilogie gedreht?",
                [("Neuseeland", true), ("Irland", false), ("Norwegen", false), ("Kanada", false)],
                "Neuseeland, unter der Regie von Peter Jackson.");

            yield return Eigenschaften(film, "Welcher Film?", "FIL", 300, Difficulty.Level3,
                "Welcher Film wird gesucht?",
                ["Er spielt größtenteils auf einem Schiff.", "Er erschien 1997.",
                 "Er gewann elf Oscars."],
                "Titanic");

            yield return Standard(natur, "Planet der Ringe", "RIN", 100, Difficulty.Level1,
                "Welcher Planet ist für sein ausgeprägtes Ringsystem bekannt?",
                ["Er ist der zweitgrößte Planet unseres Sonnensystems."],
                "Saturn");

            yield return Schaetzfrage(natur, "Landung auf dem Mond", "MON", 200, Difficulty.Level2,
                "Wann landeten zum ersten Mal Menschen auf dem Mond?",
                new DateTime(1969, 7, 20), "20. Juli 1969");

            yield return MultipleChoice(geo, "Längster Fluss", "FLU", 300, Difficulty.Level3,
                "Welcher Fluss gilt als der längste der Erde?",
                [("Nil", true), ("Amazonas", false), ("Jangtse", false), ("Mississippi", false)],
                "Der Nil, je nach Messweise dicht gefolgt vom Amazonas.");
        }

        private static void Grunddaten(
            QuestionBase frage, Guid kategorieId, string bezeichnung, string kurz,
            int punkte, Difficulty stufe)
        {
            frage.Id = Guid.NewGuid();
            frage.CategoryId = kategorieId;
            frage.Designation = $"{Marke} {bezeichnung}";
            frage.DesignationShort = kurz;
            frage.Points = punkte;
            frage.MinusPoints = punkte / 2;
            frage.Difficulty = stufe;
        }

        /// <summary>Standardfrage: Startschritt, Hinweise, Abschluss mit der Loesung.</summary>
        private static QuestionBase Standard(
            Guid kategorieId, string bezeichnung, string kurz, int punkte, Difficulty stufe,
            string frageText, string[] hinweise, string loesung)
        {
            var frage = new DefaultQuestion();
            Grunddaten(frage, kategorieId, bezeichnung, kurz, punkte, stufe);
            frage.QuestionText = frageText;

            frage.Steps.Add(Schritt(frage.Id, 1, frageText, istStart: true));

            for (var i = 0; i < hinweise.Length; i++)
                frage.Steps.Add(Schritt(frage.Id, (i + 1) * 10, hinweise[i]));

            frage.Steps.Add(Schritt(frage.Id, 900, loesung, istLoesung: true, istAbschluss: true));

            return frage;
        }

        /// <summary>
        /// Eigenschaftsfrage: die Hinweise sind die normalen Schritte, und mit jedem sinken die
        /// erreichbaren Punkte.
        /// </summary>
        private static QuestionBase Eigenschaften(
            Guid kategorieId, string bezeichnung, string kurz, int punkte, Difficulty stufe,
            string frageText, string[] hinweise, string loesung)
        {
            var frage = new PropertiesQuestion();
            Grunddaten(frage, kategorieId, bezeichnung, kurz, punkte, stufe);
            frage.QuestionText = frageText;

            frage.Steps.Add(Schritt(frage.Id, 1, frageText, istStart: true));

            for (var i = 0; i < hinweise.Length; i++)
                frage.Steps.Add(Schritt(frage.Id, (i + 1) * 10, hinweise[i]));

            frage.Steps.Add(Schritt(frage.Id, 900, loesung, istLoesung: true, istAbschluss: true));

            return frage;
        }

        /// <summary>
        /// Multiple Choice: die Antwortmoeglichkeiten sind die normalen Schritte, genau eine ist
        /// die Loesung. <c>BuzzerMaxAllowedKeySelect</c> muss der Zahl der Loesungen entsprechen,
        /// sonst kann die Frage nie richtig beantwortet werden (QuestionValidator).
        /// <para>
        /// Der Abschlussschritt traegt bewusst <b>kein</b> IsResult: sonst zaehlte er als zweite
        /// Loesung und die Frage waere ungueltig.
        /// </para>
        /// </summary>
        private static QuestionBase MultipleChoice(
            Guid kategorieId, string bezeichnung, string kurz, int punkte, Difficulty stufe,
            string frageText, (string Text, bool IstLoesung)[] optionen, string aufloesung)
        {
            var frage = new MultipleChoiceQuestion();
            Grunddaten(frage, kategorieId, bezeichnung, kurz, punkte, stufe);
            frage.QuestionText = frageText;
            frage.BuzzerMaxAllowedKeySelect = optionen.Count(o => o.IstLoesung);
            frage.ShowTextOnKeySelect = true;

            frage.Steps.Add(Schritt(frage.Id, 1, frageText, istStart: true));

            for (var i = 0; i < optionen.Length; i++)
                frage.Steps.Add(Schritt(frage.Id, (i + 1) * 10, optionen[i].Text,
                    istLoesung: optionen[i].IstLoesung));

            frage.Steps.Add(Schritt(frage.Id, 900, aufloesung, istAbschluss: true));

            return frage;
        }

        /// <summary>Schaetzfrage mit Zahlenwert. Vertraegt genau einen Loesungsschritt.</summary>
        private static QuestionBase Schaetzfrage(
            Guid kategorieId, string bezeichnung, string kurz, int punkte, Difficulty stufe,
            string frageText, double sollwert, AppreciateValueKind art, AppreciateUnit einheit,
            string aufloesung)
        {
            var frage = new AppreciateQestion();
            Grunddaten(frage, kategorieId, bezeichnung, kurz, punkte, stufe);
            frage.QuestionText = frageText;
            frage.ValueKind = art;
            frage.Unit = einheit;
            frage.ExpectedValue = sollwert;

            frage.Steps.Add(Schritt(frage.Id, 1, frageText, istStart: true));
            frage.Steps.Add(Schritt(frage.Id, 900, aufloesung, istLoesung: true, istAbschluss: true));

            return frage;
        }

        /// <summary>Schaetzfrage mit Datum.</summary>
        private static QuestionBase Schaetzfrage(
            Guid kategorieId, string bezeichnung, string kurz, int punkte, Difficulty stufe,
            string frageText, DateTime solldatum, string aufloesung)
        {
            var frage = new AppreciateQestion();
            Grunddaten(frage, kategorieId, bezeichnung, kurz, punkte, stufe);
            frage.QuestionText = frageText;
            frage.ValueKind = AppreciateValueKind.Date;
            frage.Unit = AppreciateUnit.Datum;
            frage.ExpectedDate = solldatum;

            frage.Steps.Add(Schritt(frage.Id, 1, frageText, istStart: true));
            frage.Steps.Add(Schritt(frage.Id, 900, aufloesung, istLoesung: true, istAbschluss: true));

            return frage;
        }

        /// <summary>
        /// Baut das Spiel: vier Spalten (die Kategorien), drei Zeilen (die Punktestufen), zwoelf
        /// belegte Zellen, vier Mitspieler und der Moderator.
        /// </summary>
        private static async Task<Game> CreateGameAsync(
            List<Category> kategorien, List<QuestionBase> fragen, List<Player> spieler)
        {
            var moderator = spieler[^1];
            var mitspieler = spieler[..^1];

            var nummer = await NaechsteNummerAsync();

            var spiel = new Game
            {
                Id = Guid.NewGuid(),
                Designation = $"{Marke} Quizabend {nummer}",
                Width = 4,
                Height = 3,
                CellWidth = 200,
                CellHeight = 110,
                Phase = 1,
                CurrentRound = 0,
                SuggestedPhases = 2,
                State = GameState.Building,
                ModeratorPlayerId = moderator.Id,
            };

            using (var ctrl = new GamesController())
            {
                await ctrl.UpsertAsync(spiel);
                await ctrl.SaveChangesAsync();
            }

            using (var ctrlZellen = new GameGridCoordinatesController())
            using (var ctrlKopf = new HeadersController(ctrlZellen))
            using (var ctrlLink = new PlayerXGamesController(ctrlZellen))
            {
                for (var spalte = 0; spalte < 4; spalte++)
                {
                    await ctrlKopf.InsertAsync(new Header
                    {
                        Id = Guid.NewGuid(),
                        GameId = spiel.Id,
                        HeaderType = HeaderType.Column,
                        Index = spalte + 1,
                        Designation = kategorien[spalte].Designation.Replace($"{Marke} ", string.Empty),
                    });
                }

                for (var zeile = 0; zeile < 3; zeile++)
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

                // Die Fragen liegen in der Reihenfolge vor, in der BaueFragen sie erzeugt hat:
                // je Kategorie drei, aufsteigend. Sie werden spaltenweise verteilt.
                var nachKategorie = kategorien
                    .Select(k => fragen.Where(f => f.CategoryId == k.Id)
                        .OrderBy(f => f.Points)
                        .ToList())
                    .ToList();

                for (var spalte = 0; spalte < 4; spalte++)
                {
                    for (var zeile = 0; zeile < 3; zeile++)
                    {
                        var frage = nachKategorie[spalte].ElementAtOrDefault(zeile);

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

                        // Die Punkte sind gespeicherte Spalten - ohne diesen Aufruf stuende
                        // in jeder Zelle eine Null.
                        zelle.CalculateAndSetCurrentPoints();

                        // Die Rueckverweise duerfen nicht mit in den Schreibvorgang.
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
            }

            return spiel;
        }

        /// <summary>Zaehlt vorhandene Demo-Spiele, damit ein zweiter Lauf nicht kollidiert.</summary>
        private static async Task<int> NaechsteNummerAsync()
        {
            using var ctrl = new GamesController();

            var spiele = await ctrl.GetAllAsync();

            return spiele.Count(g => g.Designation.StartsWith(Marke, StringComparison.Ordinal)) + 1;
        }
    }
}
