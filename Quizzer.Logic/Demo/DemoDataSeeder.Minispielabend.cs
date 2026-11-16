using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.Logic.Demo
{
    /// <summary>
    /// Der Minispielabend (M35 C8): ein Spiel mit jeder Minispiel-Art und jeder Sonderregel, nach Themen in Spalten,
    /// an einem Abend durchspielbar. Zwei Demo-Spieler halten je einen offenen Joker (einer davon die Sperre), damit die
    /// Joker-Runde an den gewöhnlichen Fragen vorkommt. Inhalt: <c>DemoDataSeeder.MinispielabendInhalt.cs</c>.
    /// </summary>
    public static partial class DemoDataSeeder
    {
        /// <summary>Wie das Spiel heißt - mit laufender Nummer dahinter.</summary>
        public const string MinispielabendName = "Minispielabend";

        /// <summary>Der Grund der beiden Demo-Joker.</summary>
        public const string DemoJokerGrund = "Demo";

        /// <summary>Was der Minispielabend angelegt hat.</summary>
        /// <param name="Spiel">Das angelegte Spiel samt Zellen.</param>
        /// <param name="Fragen">Anzahl neuer Fragen.</param>
        /// <param name="Joker">Die vergebenen, offenen Joker.</param>
        public sealed record MinispielabendErgebnis(Game Spiel, int Fragen, IReadOnlyList<PlayerJoker> Joker);

        /// <summary>
        /// Legt den Minispielabend an. Die Demo-Mitspieler werden wiederverwendet; jeder Lauf legt ein weiteres,
        /// durchnummeriertes Spiel samt eigener Fragen an.
        /// </summary>
        public static async Task<MinispielabendErgebnis> CreateMinispielabendAsync()
        {
            var kategorie = await MinispielabendKategorieAsync();
            var spieler = await DemoSpielerAsync();
            var spalten = BaueMinispielabend(kategorie.Id).ToList();

            foreach (var zelle in spalten.SelectMany(s => s.Zellen))
            {
                zelle.Frage.OwnerPlayerId = spieler[^1].Id;
                await SaveQuestionAsync(zelle.Frage);
            }

            var spiel = await MinispielabendSpielAsync(spalten, spieler);
            var joker = await DemoJokerAsync(spiel.Id, spieler[..^1]);

            return new MinispielabendErgebnis(spiel, spalten.Sum(s => s.Zellen.Count), joker);
        }

        private static async Task<Category> MinispielabendKategorieAsync()
        {
            using var ctrl = new CategoriesController();

            var kategorie = new Category { Id = Guid.NewGuid(), Designation = $"{Marke} {MinispielabendName}" };

            await ctrl.InsertAsync(kategorie);
            await ctrl.SaveChangesAsync();

            return kategorie;
        }

        /// <summary>Das Raster: je Thema eine Spalte, je Zeile 100 Punkte mehr; die Sonderregel steht an der Zelle.</summary>
        private static async Task<Game> MinispielabendSpielAsync(List<AbendSpalte> spalten, List<Player> spieler)
        {
            var zeilen = spalten.Max(s => s.Zellen.Count);

            using var spieleCtrl = new GamesController();

            var nummer = (await spieleCtrl.GetAllAsync())
                .Count(g => g.Designation.StartsWith($"{Marke} {MinispielabendName}", StringComparison.Ordinal)) + 1;

            var spiel = new Game
            {
                Id = Guid.NewGuid(),
                Designation = $"{Marke} {MinispielabendName} {nummer}",
                Width = spalten.Count,
                Height = zeilen,
                CellWidth = 200,
                CellHeight = 110,
                Phase = 1,
                CurrentRound = 0,
                SuggestedPhases = 1,
                State = GameState.Building,
                ModeratorPlayerId = spieler[^1].Id,
            };

            await spieleCtrl.UpsertAsync(spiel);
            await spieleCtrl.SaveChangesAsync();

            using var ctrlZellen = new GameGridCoordinatesController();
            using var ctrlKopf = new HeadersController(ctrlZellen);
            using var ctrlLink = new PlayerXGamesController(ctrlZellen);

            for (var spalte = 0; spalte < spalten.Count; spalte++)
                await ctrlKopf.InsertAsync(Kopf(spiel.Id, HeaderType.Column, spalte, spalten[spalte].Thema));

            for (var zeile = 0; zeile < zeilen; zeile++)
                await ctrlKopf.InsertAsync(Kopf(spiel.Id, HeaderType.Row, zeile, $"{(zeile + 1) * 100}"));

            for (var spalte = 0; spalte < spalten.Count; spalte++)
            {
                for (var zeile = 0; zeile < zeilen; zeile++)
                    await ctrlZellen.InsertAsync(AbendRasterzelle(spiel, spalte, zeile, spalten[spalte].Zellen.ElementAtOrDefault(zeile)));
            }

            foreach (var p in spieler[..^1])
                await ctrlLink.InsertAsync(new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = p.Id });

            await ctrlZellen.SaveChangesAsync();

            return spiel;
        }

        private static Header Kopf(Guid spielId, HeaderType art, int index, string text)
            => new() { Id = Guid.NewGuid(), GameId = spielId, HeaderType = art, Index = index + 1, Designation = text };

        private static GameGridCoordinate AbendRasterzelle(Game spiel, int spalte, int zeile, AbendZelle? inhalt)
        {
            var zelle = new GameGridCoordinate
            {
                Id = Guid.NewGuid(),
                GameId = spiel.Id,
                Game = spiel,
                X = spalte,
                Y = zeile,
                Phase = 1,
                QuestionBaseId = inhalt?.Frage.Id,
                QuestionBase = inhalt?.Frage,
                SpecialRule = inhalt?.Regel ?? CellSpecialRule.None,
                SpecialRuleAnnounced = inhalt?.Angekuendigt ?? false,
                SpecialRuleVariant = inhalt?.Spielweise ?? 0,
            };

            // Die Punkte sind gespeicherte Spalten - ohne diesen Aufruf stuende in jeder Zelle eine Null.
            zelle.CalculateAndSetCurrentPoints();
            spiel.GameGridCoordinates.Add(zelle);

            return zelle;
        }

        /// <summary>Die ersten beiden Mitspieler bekommen je einen offenen Joker: die Sperre und „Doppelt".</summary>
        private static async Task<IReadOnlyList<PlayerJoker>> DemoJokerAsync(Guid spielId, List<Player> mitspieler)
        {
            using var ctrl = new PlayerJokersController();

            var jetzt = DateTimeOffset.Now;
            var joker = new List<PlayerJoker>
            {
                await ctrl.GrantAsync(spielId, mitspieler[0].Id, JokerKind.Block, DemoJokerGrund, jetzt),
                await ctrl.GrantAsync(spielId, mitspieler[1].Id, JokerKind.Double, DemoJokerGrund, jetzt),
            };

            await ctrl.SaveChangesAsync();

            return joker;
        }
    }
}
