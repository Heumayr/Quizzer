using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Base;
using Quizzer.Views.Teams;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Quizzer.UnitTests.Views.Teams
{
    /// <summary>
    /// Der Dialog mit Inhalt ausgelegt (M37 T2): die Vorlagen der Zeilen binden ohne tote Bindung, und kein Team steht
    /// nur als Farbe da - Symbol und Name sind zu sehen (<c>standards-bedienbarkeit.md</c> Regel 3). Der leere Aufbau in
    /// <c>AllWindowsBuildUnitTests</c> sieht die Vorlagen nicht.
    /// </summary>
    [TestClass]
    public class TeamsDialogRenderUnitTests
    {
        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
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

        private static Game Spiel()
        {
            var spiel = new Game { Id = Guid.NewGuid(), Designation = "Teamabend", IsTeamGame = true };
            var rot = new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Rote Füchse", ColorIndex = 5, SortOrder = 0 };
            var schwarz = new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Nachteulen", ColorIndex = 7, SortOrder = 1 };
            spiel.GameTeams.AddRange([rot, schwarz]);

            foreach (var (name, team) in new[] { ("Anna", rot), ("Bert", schwarz), ("Carla", (GameTeam?)null) })
            {
                var spieler = new Player { Id = Guid.NewGuid(), Designation = name };
                spiel.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = spiel.Id, PlayerId = spieler.Id, Player = spieler, GameTeamId = team?.Id });
            }

            return spiel;
        }

        [TestMethod]
        public void TheRowsBindAndNoTeamStandsAsColourAlone()
        {
            UiTestHost.Run(() =>
            {
                using var wache = new BindingErrorWatch();
                var fenster = new TeamsView();

                try
                {
                    ((TeamsViewModel)fenster.DataContext).Lade(Spiel(), "Teams bearbeiten");

                    var inhalt = (FrameworkElement)fenster.Content;
                    inhalt.Measure(new Size(680, 800));
                    inhalt.Arrange(new Rect(0, 0, 680, 800));
                    inhalt.UpdateLayout();

                    var namen = Descendants<TextBox>(inhalt).Select(t => t.Text).ToList();
                    var texte = Descendants<TextBlock>(inhalt).Select(t => t.Text).ToList();

                    CollectionAssert.IsSubsetOf(new[] { "Rote Füchse", "Nachteulen" }, namen, "Ein Teamname fehlt im Dialog.");
                    CollectionAssert.IsSubsetOf(new[] { "✚", "⬢" }, texte, "Ein Team steht ohne sein Symbol da - nur als Farbe.");
                    CollectionAssert.IsSubsetOf(new[] { "Anna", "Bert", "Carla" }, texte, "Ein Mitspieler fehlt in der Zuteilung.");
                    Assert.IsTrue(Descendants<TextBox>(inhalt).Where(t => t.Text == "Rote Füchse").All(t => t.MaxLength == GameTeam.MaxNameLength),
                        "Das Namensfeld trägt nicht die Grenze des Modells.");
                    Assert.AreEqual(0, wache.Errors.Count, "Bindungen des Dialogs greifen ins Leere: " + string.Join(" || ", wache.Errors));
                }
                finally
                {
                    fenster.Close();
                }
            });
        }
    }
}
