using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Base;
using Quizzer.Views.GameViews;
using Quizzer.Views.GameViews.Sub;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Die Teams am Beamer (M37 T3): im Teamspiel Teamkacheln statt Spielerkacheln - Fläche, Symbol, Name und die Summe über
    /// den Stempel, darunter die Mitglieder klein; wer ohne Team ist, steht in „Ohne Team" mit dem Satz, dass es für kein
    /// Team zählt. Gemessen an der wirklich ausgelegten Leiste (<see cref="UcStatsView"/>), wie Beamer und Spielleiterleiste
    /// sie teilen.
    /// </summary>
    [TestClass]
    public class TeamBeamerUnitTests
    {
        private const double LeisteBreite = 1900;
        private const double LeisteHoehe = 200;

        private Game spiel = null!;
        private GameGridCoordinate zelle = null!;
        private GameTeam rot = null!;
        private GameTeam blau = null!;

        [TestInitialize]
        public void SetUp()
        {
            spiel = new Game { Id = Guid.NewGuid(), Designation = "Teamabend", IsTeamGame = true, Phase = 1, SuggestedPhases = 1 };
            zelle = new GameGridCoordinate { Id = Guid.NewGuid(), GameId = spiel.Id, QuestionBaseId = Guid.NewGuid() };
            spiel.GameGridCoordinates.Add(zelle);
            rot = new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Rot", ColorIndex = 5, SortOrder = 0 };
            blau = new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Blau", ColorIndex = 4, SortOrder = 1 };
            spiel.GameTeams.AddRange([rot, blau]);
        }

        private PlayerXGame Setze(string name, GameTeam? team, int punkte, Guid? stempel)
        {
            var spieler = new Player { Id = Guid.NewGuid(), Designation = name, DisplayName = name };
            var platz = new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = spieler.Id, Player = spieler, GameTeamId = team?.Id };
            spiel.PlayerXGames.Add(platz);
            zelle.QuestionResults.Add(new QuestionResult { Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = spieler.Id, Score = punkte, GameTeamId = stempel });
            return platz;
        }

        /// <summary>Anna und Bert bei Rot, Carl bei Blau, Dora am Telefon beigetreten (M10): ohne Team, ohne Stempel.</summary>
        private PlayerXGame Abend()
        {
            Setze("Anna", rot, 300, rot.Id);
            Setze("Bert", rot, 100, rot.Id);
            Setze("Carl", blau, 200, blau.Id);
            return Setze("Dora", null, 150, null);
        }

        private StatsContext Stats()
        {
            var leiter = new GameMasterViewModel { Game = spiel };
            var stats = leiter.StatsContext;
            stats.GameMasterViewModel = leiter;
            stats.Game = spiel;
            stats.PlayerStatsContextList = spiel.Players
                .Select(p => new PlayerStatsContext { Player = p, Game = spiel, StatsContext = stats, GameMasterViewModel = leiter })
                .ToList();
            stats.UpdateScores(leiter.IsGameFinished);
            return stats;
        }

        private static UcStatsView Leiste(StatsContext stats)
        {
            var ansicht = new UcStatsView { DataContext = stats };
            ansicht.Measure(new Size(LeisteBreite, LeisteHoehe));
            ansicht.Arrange(new Rect(0, 0, LeisteBreite, LeisteHoehe));
            ansicht.UpdateLayout();
            return ansicht;
        }

        internal static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);

                if (child is T hit)
                    yield return hit;

                foreach (var deeper in Descendants<T>(child))
                    yield return deeper;
            }
        }

        /// <summary>Ohne Fenster ist <c>IsVisible</c> immer falsch - also selbst nachsehen, ob es und alle Eltern sichtbar sind.</summary>
        internal static bool Sichtbar(DependencyObject element)
        {
            for (var e = element; e != null; e = VisualTreeHelper.GetParent(e))
            {
                if (e is UIElement { Visibility: not Visibility.Visible })
                    return false;
            }

            return true;
        }

        private static List<string> Texte(DependencyObject wurzel) =>
            Descendants<TextBlock>(wurzel).Where(t => Sichtbar(t) && !string.IsNullOrEmpty(t.Text)).Select(t => t.Text).ToList();

        /// <summary>Wie hoch ein Text nach allen Viewboxen auf dem Bildschirm ankommt (wie <c>PlayerScoreTileRenderUnitTests</c>).</summary>
        private static double EffektiveHoehe(TextBlock text)
        {
            var hoehe = text.ActualHeight;

            for (DependencyObject? e = text; e != null; e = VisualTreeHelper.GetParent(e))
            {
                if (e is Visual visual && VisualTreeHelper.GetTransform(visual) is ScaleTransform skala)
                    hoehe *= skala.ScaleY;
            }

            return hoehe;
        }

        [TestMethod]
        public void TheTeamGameShowsTeamTilesWithSumAndMembers()
        {
            UiTestHost.Run(() =>
            {
                Abend();
                using var wache = new BindingErrorWatch();
                var ansicht = Leiste(Stats());

                var kacheln = Descendants<UcTeamScoreTile>(ansicht).Where(Sichtbar).ToList();
                Assert.AreEqual(3, kacheln.Count, "Erwartet: Rot, Blau und „Ohne Team“.");
                Assert.IsFalse(Descendants<UcPlayerScoreTile>(ansicht).Any(Sichtbar), "Im Teamspiel stehen noch die Spielerkacheln in der Leiste.");

                CollectionAssert.IsSubsetOf(new[] { "✚", "Rot", "400", "Anna", "300", "Bert", "100" }, Texte(kacheln[0]), "Rot: " + string.Join(" ", Texte(kacheln[0])));
                CollectionAssert.IsSubsetOf(new[] { "★", "Blau", "200", "Carl" }, Texte(kacheln[1]), "Blau: " + string.Join(" ", Texte(kacheln[1])));
                CollectionAssert.IsSubsetOf(new[] { "Ohne Team", TeamKachel.OhneTeamText, "Dora", "150" }, Texte(kacheln[2]),
                    "Wer ohne Team ist, steht nicht mit dem Satz da, dass es für kein Team zählt: " + string.Join(" ", Texte(kacheln[2])));
                Assert.AreEqual(0, wache.Errors.Count, "Bindungen der Teamkachel greifen ins Leere: " + string.Join(" || ", wache.Errors));
            });
        }

        /// <summary>Gegenrichtung: ohne Teamspiel bleibt die Leiste, wie sie war - auch wenn Teams und Stempel noch dastehen.</summary>
        [TestMethod]
        public void WithoutTeamGameThePlayerTilesStay()
        {
            UiTestHost.Run(() =>
            {
                Abend();
                spiel.IsTeamGame = false;
                var ansicht = Leiste(Stats());

                Assert.AreEqual(4, Descendants<UcPlayerScoreTile>(ansicht).Count(Sichtbar));
                Assert.IsFalse(Descendants<UcTeamScoreTile>(ansicht).Any(Sichtbar), "Ohne Teamspiel stehen Teamkacheln in der Leiste.");
            });
        }

        /// <summary>A10: ein Teamspiel ohne ein einziges Team (der Start verhindert es) fällt auf die Spielerkacheln zurück.</summary>
        [TestMethod]
        public void ATeamGameWithoutTeamsKeepsThePlayerTiles()
        {
            UiTestHost.Run(() =>
            {
                Abend();
                spiel.GameTeams.Clear();
                var ansicht = Leiste(Stats());

                Assert.AreEqual(4, Descendants<UcPlayerScoreTile>(ansicht).Count(Sichtbar), "Ohne Teams steht die Leiste leer.");
                Assert.IsFalse(Descendants<UcTeamScoreTile>(ansicht).Any(Sichtbar));
            });
        }

        /// <summary>A10: eine Farbe außerhalb der Palette (der Server weist sie ab, ein alter Stand vielleicht nicht) bricht die Kachel nicht.</summary>
        [TestMethod]
        public void AColorOutsideThePaletteDoesNotBreakTheTile()
        {
            UiTestHost.Run(() =>
            {
                Abend();
                rot.ColorIndex = 42;
                using var wache = new BindingErrorWatch();
                var ansicht = Leiste(Stats());

                var kachel = Descendants<UcTeamScoreTile>(ansicht).First(Sichtbar);
                CollectionAssert.IsSubsetOf(new[] { "–", "Rot", "400" }, Texte(kachel));
                Assert.AreEqual(0, wache.Errors.Count, string.Join(" || ", wache.Errors));
            });
        }

        /// <summary>Steht der Punktestand links (Nutzerwunsch 2026-10-21), stehen dort ebenso die Teams, nicht die Spieler.</summary>
        [TestMethod]
        public void TheLeftPlacementShowsTheTeamsToo()
        {
            UiTestHost.Run(() =>
            {
                Abend();
                var stats = Stats();
                var beamer = new GamePlayerViewModel { GameMasterViewModel = stats.GameMasterViewModel, Game = spiel, ScoresLeft = true };
                var fenster = new GamePlayerView { DataContext = beamer };
                var inhalt = (FrameworkElement)fenster.Content;
                inhalt.Measure(new Size(1920, 1080));
                inhalt.Arrange(new Rect(0, 0, 1920, 1080));
                inhalt.UpdateLayout();

                Assert.AreEqual(Visibility.Visible, beamer.ShowLeftStats, "Der Punktestand steht nicht links - dann misst der Test nichts.");
                Assert.AreEqual(3, Descendants<UcTeamScoreTile>(inhalt).Count(Sichtbar), "Links fehlen die Teamkacheln.");
                Assert.IsFalse(Descendants<UcPlayerScoreTile>(inhalt).Any(Sichtbar), "Links stehen im Teamspiel die Spielerkacheln.");
            });
        }

        /// <summary>Vier Teams in der Leiste eines 1080er Beamers: die Summe muss vom Sofa aus lesbar sein.</summary>
        [TestMethod]
        public void TheTeamSumIsBigEnoughToReadFromTheCouch()
        {
            UiTestHost.Run(() =>
            {
                spiel.GameTeams.Add(new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Gelb", ColorIndex = 3, SortOrder = 2 });
                spiel.GameTeams.Add(new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Die mit dem langen Namen", ColorIndex = 2, SortOrder = 3 });
                foreach (var (team, i) in spiel.GameTeams.Select((t, i) => (t, i)))
                {
                    Setze($"Spieler {i}a", team, 600, team.Id);
                    Setze($"Spieler {i}b", team, 600, team.Id);
                    Setze($"Spieler {i}c", team, 0, team.Id);
                }

                var ansicht = Leiste(Stats());
                var summen = Descendants<TextBlock>(ansicht).Where(t => Sichtbar(t) && t.Text == "1200").Select(EffektiveHoehe).ToList();

                Assert.AreEqual(4, summen.Count, "Nicht jede Teamsumme steht in der Leiste.");
                Assert.IsTrue(summen.Min() >= 40, $"Die kleinste Teamsumme ist nur {summen.Min():0.#} Bildpunkte hoch - aus mehreren Metern nicht zu lesen.");
            });
        }

        /// <summary>
        /// F-M37-07 am Beamer: wer umgesetzt wird, lässt seine Punkte beim alten Team - und steht danach in der Kachel des
        /// neuen. Der Verlauf wird gemessen: erst die Kacheln, dann umsetzen, dann dieselbe Leiste neu lesen.
        /// </summary>
        [TestMethod]
        public void MovingAPlayerLeavesThePointsWithTheOldTeamOnTheBeamer()
        {
            UiTestHost.Run(() =>
            {
                Abend();
                var stats = Stats();
                Assert.AreEqual(400, stats.TeamKacheln[0].Summe, "Vor dem Umsetzen zählt Rot nicht 400 - dann misst der Test nichts.");

                var anna = spiel.PlayerXGames.Single(x => x.Player.DisplayName == "Anna");
                anna.GameTeamId = blau.Id;
                stats.TeamsAktualisieren(siegerehrung: false);

                var rotKachel = stats.TeamKacheln.Single(k => k.Team == rot);
                var blauKachel = stats.TeamKacheln.Single(k => k.Team == blau);
                Assert.AreEqual(400, rotKachel.Summe, "Das Umsetzen hat Annas Punkte von Rot abgezogen.");
                Assert.AreEqual(200, blauKachel.Summe, "Blau zählt Annas Punkte aus der Zeit bei Rot.");
                CollectionAssert.AreEqual(new[] { "Anna", "Carl" }, blauKachel.Mitglieder.Select(m => m.PlayerName).ToArray(), "Anna steht nach dem Umsetzen nicht bei Blau.");
                CollectionAssert.AreEqual(new[] { "Bert" }, rotKachel.Mitglieder.Select(m => m.PlayerName).ToArray());
            });
        }

        /// <summary>„Teams umsetzen“ im Spielfenster (T2) zieht die Kacheln am Beamer nach, nicht erst der nächste Zug.</summary>
        [TestMethod]
        public void AfterMovingTeamsTheBeamerFollowsAtOnce()
        {
            UiTestHost.Run(() =>
            {
                Abend();
                var leiter = new GameMasterViewModel { Game = spiel };
                var stats = leiter.StatsContext;
                stats.GameMasterViewModel = leiter;
                stats.Game = spiel;
                stats.PlayerStatsContextList = spiel.Players
                    .Select(p => new PlayerStatsContext { Player = p, Game = spiel, StatsContext = stats, GameMasterViewModel = leiter })
                    .ToList();
                stats.UpdateScores(false);

                spiel.PlayerXGames.Single(x => x.Player.DisplayName == "Bert").GameTeamId = blau.Id;
                leiter.TeamsAnzeigeAktualisieren();

                CollectionAssert.AreEqual(new[] { "Bert", "Carl" }, stats.TeamKacheln.Single(k => k.Team == blau).Mitglieder.Select(m => m.PlayerName).ToArray(),
                    "Nach dem Umsetzen zeigt der Beamer bis zum nächsten Zug die alte Aufstellung.");
            });
        }

        /// <summary>Wer am Telefon beitritt und dann zugeteilt wird: seine Ergebnisse ohne Stempel zählen beim neuen Team.</summary>
        [TestMethod]
        public void AJoinerWithoutStampCountsForTheTeamHeIsAssignedTo()
        {
            UiTestHost.Run(() =>
            {
                var dora = Abend();
                var stats = Stats();
                Assert.IsTrue(stats.TeamKacheln.Last().IstOhneTeam, "Vor dem Zuteilen fehlt die Gruppe „Ohne Team“.");

                dora.GameTeamId = blau.Id;
                stats.TeamsAktualisieren(siegerehrung: false);

                Assert.AreEqual(350, stats.TeamKacheln.Single(k => k.Team == blau).Summe, "Doras Ergebnis ohne Stempel zählt nicht beim neuen Team.");
                Assert.IsFalse(stats.TeamKacheln.Any(k => k.IstOhneTeam), "Nach dem Zuteilen steht „Ohne Team“ noch da.");
            });
        }
    }
}
