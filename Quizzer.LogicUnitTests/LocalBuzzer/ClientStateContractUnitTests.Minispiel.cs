using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// Derselbe Vertrag eine Ebene tiefer (M35 B3): was im <c>layoutInfo</c> des Minispiels steckt,
    /// was das Telefon ueber <c>SpielAktion</c> zurueckschickt, und die Zahlen der Aufzaehlungen, die
    /// <c>constants.js</c> nachbildet. Auch hier faellt ein umbenanntes Feld still auf den Rueckfall.
    /// </summary>
    public partial class ClientStateContractUnitTests
    {
        private static string ReadScript(params string[] teile) =>
            File.ReadAllText(Path.Combine([ScriptFolder, .. teile]));

        private static IEnumerable<string> Felder(Type typ) =>
            typ.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => CamelCase(p.Name));

        /// <summary>Alle Zugriffe <c>variable.feld</c> bzw. <c>variable?.feld</c> im Skript.</summary>
        private static List<string> Zugriffe(string skript, string variable) =>
            Regex.Matches(skript, $@"\b{variable}\??\.([A-Za-z_][A-Za-z0-9_]*)")
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .ToList();

        [TestMethod]
        [DataRow(typeof(MinigameClientInfo), "rahmen.js", "serverInfo")]
        [DataRow(typeof(ReactionClientInfo), "reaktion.js", "artInfo")]
        [DataRow(typeof(TapSprintClientInfo), "tippsprint.js", "artInfo")]
        [DataRow(typeof(StopwatchClientInfo), "stoppuhr.js", "artInfo")]
        [DataRow(typeof(DuelClientInfo), "duell.js", "artInfo")]
        [DataRow(typeof(RobberClientInfo), "raeuber.js", "artInfo")]
        [DataRow(typeof(ShareOrTakeClientInfo), "teilen.js", "artInfo")]
        [DataRow(typeof(MinigamePlayerOption), "spielerwahl.js", "spieler")]
        [DataRow(typeof(MinigamePlayerOption), "raeuber.js", "spieler")]
        public void TheMinigameModulesReadExactlyTheFieldsTheServerSends(Type typ, string datei, string variable)
        {
            var skript = ReadScript("minispiele", datei);
            var zugriffe = Zugriffe(skript, variable);

            Assert.IsTrue(zugriffe.Count > 0,
                $"Kein Zugriff auf {variable} in {datei} - das Muster greift nicht, die Zusicherung waere immer gruen.");

            var nieGelesen = Felder(typ).Where(f => !zugriffe.Contains(f)).ToList();
            var unbekannt = zugriffe.Except(Felder(typ)).ToList();

            Assert.AreEqual(0, nieGelesen.Count,
                $"{typ.Name} schickt, aber {datei} liest es nie: " + string.Join(", ", nieGelesen));
            Assert.AreEqual(0, unbekannt.Count,
                $"{datei} liest von {variable}, was {typ.Name} nicht hat - immer undefined: " + string.Join(", ", unbekannt));
        }

        /// <summary>Die Rueckrichtung: was das Telefon schickt, muss am Server so heissen.</summary>
        [TestMethod]
        [DataRow(typeof(MinigameAction), "rahmen.js")]
        [DataRow(typeof(ReactionPayload), "reaktion.js")]
        [DataRow(typeof(TapSprintPayload), "tippsprint.js")]
        [DataRow(typeof(StopwatchPayload), "stoppuhr.js")]
        [DataRow(typeof(PlayerChoicePayload), "spielerwahl.js")]
        [DataRow(typeof(DuelPayload), "duell.js")]
        [DataRow(typeof(RobberPayload), "raeuber.js")]
        [DataRow(typeof(ShareOrTakePayload), "teilen.js")]
        public void ThePhoneSendsTheFieldsTheServerReads(Type typ, string datei)
        {
            var skript = ReadScript("minispiele", datei);

            var fehlend = Felder(typ).Where(f => !Regex.IsMatch(skript, $@"\b{f}\s*[:,}}]")).ToList();

            Assert.AreEqual(0, fehlend.Count,
                $"{typ.Name} erwartet, aber {datei} schickt es nie - der Server liest den Standardwert: "
                + string.Join(", ", fehlend));
        }

        /// <summary>
        /// Jede Art, die im Telefonrahmen laeuft, hat dort ein Modul (M35 C1) - sonst zeigt das Telefon
        /// „kennt das Telefon noch nicht", und kein Test ausser dem echten Handy merkt es.
        /// </summary>
        [TestMethod]
        public void EveryFrameKindHasAPhoneModule()
        {
            var rahmen = ReadScript("minispiele", "rahmen.js");
            var registriert = Regex.Matches(rahmen, @"\[MinigameKinds\.(\w+)\s*,").Select(m => m.Groups[1].Value).ToList();

            var erwartet = MinigameProfiles.All
                .Where(p => p.Layout == BuzzerControlsLayout.Minigame)
                .Select(p => p.Kind.ToString())
                .ToList();

            Assert.IsTrue(erwartet.Count > 0, "Keine Art im Telefonrahmen - die Zusicherung waere immer gruen.");
            CollectionAssert.AreEquivalent(erwartet, registriert,
                "rahmen.js kennt nicht genau die Arten des Telefonrahmens: " + string.Join(", ", registriert));
        }

        /// <summary>Die Zahlen der Aufzaehlungen stehen in <c>constants.js</c> noch einmal - Name fuer Name gleich.</summary>
        [TestMethod]
        [DataRow(typeof(BuzzerControlsLayout), "Layouts")]
        [DataRow(typeof(MinigameKind), "MinigameKinds")]
        [DataRow(typeof(MinigamePhase), "MinigamePhases")]
        [DataRow(typeof(ReactionOutcome), "ReactionOutcomes")]
        [DataRow(typeof(SchereSteinPapier), "SchereSteinPapier")]
        [DataRow(typeof(CellSpecialRule), "SpecialRules")]
        public void TheConstantsMirrorTheServerEnums(Type aufzaehlung, string name)
        {
            var konstanten = ReadScript("constants.js");
            var block = Regex.Match(konstanten, $@"export const {name} = Object\.freeze\(\{{(?<inhalt>[^}}]*)\}}\)");

            Assert.IsTrue(block.Success, $"{name} steht nicht in constants.js.");

            var js = Regex.Matches(block.Groups["inhalt"].Value, @"(\w+)\s*:\s*(\d+)")
                .ToDictionary(m => m.Groups[1].Value, m => int.Parse(m.Groups[2].Value));

            var cs = Enum.GetValues(aufzaehlung).Cast<object>()
                .ToDictionary(w => w.ToString()!, w => Convert.ToInt32(w));

            CollectionAssert.AreEquivalent(cs.ToList(), js.ToList(),
                $"{name} in constants.js weicht von {aufzaehlung.Name} ab - JS: "
                + string.Join(", ", js.Select(e => $"{e.Key}={e.Value}")));
        }

        /// <summary>Die Disziplinen des Duells sind keine Aufzaehlung, sondern Spielweisen - dieselbe Probe von Hand (M35 C2).</summary>
        [TestMethod]
        public void TheDuelDisciplinesMirrorTheVariants()
        {
            var block = Regex.Match(ReadScript("constants.js"), @"export const DuelDisciplines = Object\.freeze\(\{(?<inhalt>[^}]*)\}\)");

            Assert.IsTrue(block.Success, "DuelDisciplines steht nicht in constants.js.");

            var js = Regex.Matches(block.Groups["inhalt"].Value, @"(\w+)\s*:\s*(\d+)")
                .ToDictionary(m => m.Groups[1].Value, m => int.Parse(m.Groups[2].Value));

            Assert.AreEqual(DuellEvaluator.SchereSteinPapierVariant, js.GetValueOrDefault("RockPaperScissors", -1));
            Assert.AreEqual(DuellEvaluator.ReaktionVariant, js.GetValueOrDefault("Reaction", -1));
            Assert.AreEqual("Reaktion", MinigameProfiles.For(MinigameKind.Duel).Variants[DuellEvaluator.ReaktionVariant].Name,
                "Die Spielweise Reaktion steht im Profil nicht dort, wo DuellEvaluator sie erwartet.");
        }
    }
}
