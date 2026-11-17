using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Buzzer;
using Quizzer.DataModels.Questions.Teams;
using System.Text.RegularExpressions;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// Derselbe Vertrag für das Team am Telefon (M37 T4): was <c>team.js</c> von <see cref="TeamClientInfo"/> liest, dass
    /// <c>index.js</c> das Feld an das Modul gibt, und dass <c>styles/team.css</c> die Palette <see cref="TeamFarben"/> spiegelt -
    /// eine zweite Liste ohne diesen Abgleich liefe still auseinander.
    /// </summary>
    public partial class ClientStateContractUnitTests
    {
        private static string StylesFolder => Path.Combine(AppContext.BaseDirectory, "wwwroot", "styles");

        [TestMethod]
        public void TheTeamModuleReadsExactlyTheFieldsTheServerSends()
        {
            // Ohne Kommentare: dort steht „team.css“, und das ist kein Feld.
            var zugriffe = Zugriffe(Regex.Replace(ReadScript("team.js"), @"//.*", string.Empty), "team");

            Assert.IsTrue(zugriffe.Count > 0, "Kein Zugriff auf team in team.js - das Muster greift nicht, die Zusicherung waere immer gruen.");
            CollectionAssert.AreEquivalent(Felder(typeof(TeamClientInfo)).ToList(), zugriffe,
                "team.js liest nicht genau, was TeamClientInfo schickt: " + string.Join(", ", zugriffe));
        }

        [TestMethod]
        public void TheIndexPageHandsTheTeamToTheModule()
        {
            var index = Regex.Replace(ReadScript("index.js"), @"//.*", string.Empty);

            StringAssert.Contains(index, "import { zeigeTeam } from \"./team.js\"");
            Assert.IsTrue(Regex.IsMatch(index, @"zeigeTeam\(dom\.team,\s*serverState\.team\b"), "index.js gibt das Team nicht an team.js.");
        }

        /// <summary>Je Platz der Palette eine Klasse mit genau ihrem Farbwert - und team.js kennt dieselbe Zahl Plätze.</summary>
        [TestMethod]
        public void TheTeamColorsMirrorThePalette()
        {
            var css = File.ReadAllText(Path.Combine(StylesFolder, "team.css"));
            var klassen = Regex.Matches(css, @"\.team-farbe-(\d+)\s*\{[^}]*--team-farbe:\s*(#[0-9A-Fa-f]{6})")
                .ToDictionary(m => int.Parse(m.Groups[1].Value), m => m.Groups[2].Value.ToUpperInvariant());

            Assert.AreEqual(TeamFarben.Alle.Count, klassen.Count, "team.css hat nicht je Farbe der Palette eine Klasse.");

            foreach (var farbe in TeamFarben.Alle)
                Assert.AreEqual(farbe.Hex.ToUpperInvariant(), klassen.GetValueOrDefault(farbe.Index), $"team-farbe-{farbe.Index} ({farbe.Name}) weicht von TeamFarben ab.");

            var anzahl = Regex.Match(ReadScript("team.js"), @"export const TEAM_FARBEN = (\d+);");
            Assert.IsTrue(anzahl.Success, "TEAM_FARBEN steht nicht in team.js.");
            Assert.AreEqual(TeamFarben.Alle.Count, int.Parse(anzahl.Groups[1].Value), "team.js kennt eine andere Zahl Teamfarben als die Palette.");
        }
    }
}
