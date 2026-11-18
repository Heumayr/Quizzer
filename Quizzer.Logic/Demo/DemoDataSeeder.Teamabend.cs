using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.Logic.Demo
{
    /// <summary>
    /// Der Teamabend (M37 T12, <c>demo-teams</c>): ein Teamspiel mit sechs Mitspielern in drei ungleich großen Teams (3, 2, 1) -
    /// jede Regel, die das Teamspiel anbietet (die vier Teamregeln, das Finale, Einsatz und Risiko), jede Art nur im Teamspiel
    /// und Minispiele beider Teamwertungen, an einem Abend durchspielbar. Zwei Mitspieler aus verschiedenen Teams halten je
    /// einen offenen Joker. Inhalt: <c>DemoDataSeeder.TeamabendInhalt.cs</c>.
    /// </summary>
    public static partial class DemoDataSeeder
    {
        /// <summary>Wie das Spiel heißt - mit laufender Nummer dahinter.</summary>
        public const string TeamabendName = "Teamabend";

        /// <summary>
        /// Die Teams des Abends: Name, Farbe (Platz in <c>TeamFarben</c>) und Mitglieder nach Anzeigename. Ungleich groß, damit
        /// das Aufteilen des Feldwerts sichtbar wird (F-M37-06): bei Rot mit Rest (100 → 34/33/33), bei Grün ungeteilt.
        /// </summary>
        public static IReadOnlyList<(string Name, int Farbe, string[] Mitglieder)> TeamabendTeams { get; } =
        [
            ("Rot", 5, ["Anna", "Bert", "Clara"]),
            ("Blau", 4, ["Dennis", "Emil"]),
            ("Grün", 2, ["Frieda"]),
        ];

        /// <summary>Was der Teamabend angelegt hat.</summary>
        /// <param name="Spiel">Das angelegte Spiel samt Zellen.</param>
        /// <param name="Fragen">Anzahl neuer Fragen.</param>
        /// <param name="Teams">Die Teams samt Zuteilung, wie sie gespeichert sind.</param>
        /// <param name="Joker">Die vergebenen, offenen Joker.</param>
        public sealed record TeamabendErgebnis(Game Spiel, int Fragen, TeamAufstellung Teams, IReadOnlyList<PlayerJoker> Joker);

        /// <summary>
        /// Legt den Teamabend an. Die Demo-Mitspieler werden wiederverwendet, die beiden fehlenden (Emil, Frieda) einmal
        /// angelegt; jeder Lauf legt ein weiteres, durchnummeriertes Spiel samt eigener Fragen an.
        /// </summary>
        public static async Task<TeamabendErgebnis> CreateTeamabendAsync()
        {
            var kategorie = await AbendKategorieAsync(TeamabendName);
            var spieler = await TeamabendSpielerAsync();
            var spalten = BaueTeamabend(kategorie.Id).ToList();

            foreach (var zelle in spalten.SelectMany(s => s.Zellen))
            {
                zelle.Frage.OwnerPlayerId = spieler[^1].Id;
                await SaveQuestionAsync(zelle.Frage);
            }

            var spiel = await AbendSpielAsync(TeamabendName, spalten, spieler, teamspiel: true);
            var teams = await TeamabendAufstellungAsync(spiel.Id, spieler);

            // Die Joker in zwei Teams: die Sperre trifft im Teamspiel nur Gegner, und das sieht man nur mit Gegnern.
            var joker = await DemoJokerAsync(spiel.Id, [Mitspieler(spieler, "Anna"), Mitspieler(spieler, "Dennis")]);

            return new TeamabendErgebnis(spiel, spalten.Sum(s => s.Zellen.Count), teams, joker);
        }

        /// <summary>Die vier Demo-Mitspieler, dazu Emil und Frieda, der Spielleiter zuletzt - fehlende entstehen einmal (V13).</summary>
        private static async Task<List<Player>> TeamabendSpielerAsync()
        {
            var spieler = await DemoSpielerAsync();

            using var ctrl = new PlayersController();
            var vorhanden = await ctrl.GetAllAsync();
            var dazu = new List<Player>();

            foreach (var kurz in new[] { "Emil", "Frieda" })
            {
                if (vorhanden.FirstOrDefault(p => p.Designation == $"{Marke} {kurz}") is { } da)
                {
                    dazu.Add(da);
                    continue;
                }

                var neu = new Player { Id = Guid.NewGuid(), Designation = $"{Marke} {kurz}", DisplayName = kurz };
                await ctrl.InsertAsync(neu);
                dazu.Add(neu);
            }

            await ctrl.SaveChangesAsync();
            spieler.InsertRange(spieler.Count - 1, dazu);

            return spieler;
        }

        private static Player Mitspieler(List<Player> spieler, string name) => spieler.Single(p => p.DisplayName == name);

        /// <summary>Teams und Zuteilung über denselben Weg wie der Spielaufbau („Teams bearbeiten …"): in einem Zug.</summary>
        private static async Task<TeamAufstellung> TeamabendAufstellungAsync(Guid spielId, List<Player> spieler)
        {
            var teams = TeamabendTeams
                .Select((t, i) => new GameTeam { Id = Guid.NewGuid(), GameId = spielId, Name = t.Name, ColorIndex = t.Farbe, SortOrder = i })
                .ToList();

            var zuteilung = TeamabendTeams
                .SelectMany((t, i) => t.Mitglieder.Select(name => (Spieler: Mitspieler(spieler, name).Id, Team: teams[i].Id)))
                .ToDictionary(z => z.Spieler, z => (Guid?)z.Team);

            using var ctrl = new GameTeamsController();
            return await ctrl.SaveLineupAsync(spielId, teams, zuteilung);
        }
    }
}
