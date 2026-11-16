using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Sonderregeln;

namespace Quizzer.LogicUnitTests.DataModels.Sonderregeln
{
    /// <summary>
    /// Sonderregeln im Raster (M35 C3, V3): zufaellig verteilen nur auf erlaubte Zellen, die Marke fuer
    /// Spielleiter und Beamer (verdeckt/angekuendigt), Bereinigen beim Speichern, Hinweis zum Spielstart.
    /// </summary>
    [TestClass]
    public class SonderregelrasterUnitTests
    {
        private static GameGridCoordinate Zelle(int x, Quizzer.DataModels.Models.QuestionBase? frage = null, bool erledigt = false)
            => new() { Id = Guid.NewGuid(), X = x, QuestionBase = frage, QuestionBaseId = frage?.Id, IsDone = erledigt };

        private static DefaultQuestion Frage() => new() { Id = Guid.NewGuid() };

        private static Verteilauftrag Auftrag(int anzahl, bool angekuendigt = false)
            => new(anzahl, angekuendigt, [CellSpecialRule.Stake, CellSpecialRule.Risk], (von, bis) => von);

        [TestMethod]
        public void DistributingOnlyHitsPlayableOrdinaryCellsWithoutARule()
        {
            var leer = Zelle(0);
            var minispiel = Zelle(1, new MinigameQuestion { Id = Guid.NewGuid() });
            var gespielt = Zelle(2, Frage(), erledigt: true);
            var schonBelegt = Zelle(3, Frage());
            schonBelegt.SpecialRule = CellSpecialRule.Risk;
            var frei = new[] { Zelle(4, Frage()), Zelle(5, Frage()), Zelle(6, Frage()) };
            var alle = new[] { leer, minispiel, gespielt, schonBelegt }.Concat(frei).ToList();

            var belegt = Sonderregelraster.Verteile(alle, Auftrag(10, angekuendigt: true));

            CollectionAssert.AreEquivalent(frei, belegt.ToList(), "Verteilt auf eine Zelle, die keine Regel tragen darf.");
            Assert.IsTrue(frei.All(z => z.SpecialRule != CellSpecialRule.None && z.SpecialRuleAnnounced));
            Assert.AreEqual(CellSpecialRule.None, leer.SpecialRule, "Leere Zelle belegt.");
            Assert.AreEqual(CellSpecialRule.None, minispiel.SpecialRule, "Minispiel belegt.");
            Assert.AreEqual(CellSpecialRule.None, gespielt.SpecialRule, "Gespielte Zelle belegt.");
            Assert.AreEqual(CellSpecialRule.Risk, schonBelegt.SpecialRule, "Eine vorhandene Regel wurde ueberschrieben.");
        }

        [TestMethod]
        public void DistributingPlacesExactlyTheRequestedNumber()
        {
            var zellen = Enumerable.Range(0, 5).Select(x => Zelle(x, Frage())).ToList();
            var wuerfe = new Queue<int>([3, 1, 0, 0]);

            var belegt = Sonderregelraster.Verteile(zellen,
                new(2, false, [CellSpecialRule.Stake, CellSpecialRule.Risk], (von, bis) => wuerfe.Dequeue()));

            Assert.AreEqual(2, belegt.Count);
            Assert.AreEqual(2, Sonderregelraster.Belegt(zellen));
            Assert.AreEqual(CellSpecialRule.Risk, zellen[3].SpecialRule, "Der Wurf bestimmt Zelle und Regel nicht.");
            Assert.AreEqual(CellSpecialRule.Stake, zellen[0].SpecialRule);
            Assert.IsFalse(zellen.Any(z => z.SpecialRuleAnnounced), "Verdeckt verteilt, aber angekuendigt.");
        }

        [TestMethod]
        public void RemovingClearsRuleAndAnnouncement()
        {
            var zelle = Zelle(0, Frage());
            zelle.SpecialRule = CellSpecialRule.Stake;
            zelle.SpecialRuleAnnounced = true;
            zelle.SpecialRuleShare = 30;
            zelle.SpecialRuleVariant = 1;

            Assert.AreEqual(1, Sonderregelraster.Entferne([zelle, Zelle(1, Frage())]).Count);
            Assert.AreEqual(CellSpecialRule.None, zelle.SpecialRule);
            Assert.IsFalse(zelle.SpecialRuleAnnounced);
            Assert.AreEqual(0, zelle.SpecialRuleShare, "Der Anteil der alten Regel bleibt stehen.");
            Assert.AreEqual(0, zelle.SpecialRuleVariant, "Die Spielweise der alten Regel bleibt stehen.");
        }

        /// <summary>Anteil und Spielweise (M35 C3b): die Zelle schlaegt die Vorgabe, 0 heisst Vorgabe; der Regeltext traegt beides.</summary>
        [TestMethod]
        public void TheRulesTextCarriesTheShareAndTheVariantOfTheCell()
        {
            var profil = new SpecialRuleProfile(CellSpecialRule.Stake, "Probe", "ohne Spielweise")
            {
                DefaultShare = 50,
                Variants = [new("Eins", "Erste: {Anteil} %"), new("Zwei", "Zweite: {Anteil} %")],
            };

            Assert.AreEqual("Zweite: 20 %", profil.RulesTextFor(1, 20));
            Assert.AreEqual("Erste: 20 %", profil.RulesTextFor(7, 20), "Eine unbekannte Spielweise liest sich nicht wie die erste.");
            Assert.AreEqual("Erste: 20 %", profil.RulesTextFor(-1, 20));
            Assert.AreEqual("ohne {Anteil}".Replace("{Anteil}", "5"),
                new SpecialRuleProfile(CellSpecialRule.Risk, "P", "ohne {Anteil}").RulesTextFor(0, 5));
            Assert.IsTrue(profil.ShowShare && profil.ShowVariant);

            var zelle = Zelle(0, Frage());
            zelle.SpecialRule = CellSpecialRule.Stake;
            Assert.AreEqual(0, SpecialRuleProfiles.ShareOf(zelle), "Einsatz kennt keinen Anteil - die Vorgabe ist 0.");
            zelle.SpecialRuleShare = 35;
            Assert.AreEqual(35, SpecialRuleProfiles.ShareOf(zelle), "Der eigene Anteil der Zelle zaehlt nicht.");
            zelle.SpecialRule = (CellSpecialRule)99;
            Assert.AreEqual(string.Empty, SpecialRuleProfiles.RulesTextOf(zelle));
        }

        [TestMethod]
        public void TheBeamerShowsOnlyAnnouncedRulesTheGameMasterAll()
        {
            var verdeckt = Zelle(0, Frage());
            verdeckt.SpecialRule = CellSpecialRule.Stake;
            var angekuendigt = Zelle(1, Frage());
            angekuendigt.SpecialRule = CellSpecialRule.Risk;
            angekuendigt.SpecialRuleAnnounced = true;
            var ohne = Zelle(2, Frage());

            Assert.AreEqual(string.Empty, Sonderregelraster.MarkeBeamer(verdeckt), "Eine verdeckte Regel steht am Beamer.");
            Assert.AreEqual($"{SpecialRuleProfiles.Symbol} Risiko", Sonderregelraster.MarkeBeamer(angekuendigt));
            Assert.AreEqual($"{SpecialRuleProfiles.Symbol} Einsatz · verdeckt", Sonderregelraster.MarkeSpielleiter(verdeckt));
            Assert.AreEqual($"{SpecialRuleProfiles.Symbol} Risiko · angekündigt", Sonderregelraster.MarkeSpielleiter(angekuendigt));
            Assert.AreEqual(string.Empty, Sonderregelraster.MarkeSpielleiter(ohne));
            Assert.AreEqual(string.Empty, Sonderregelraster.MarkeBeamer(ohne));
        }

        [TestMethod]
        public void SavingRemovesARuleFromAMinigameOrAnEmptyCellOnly()
        {
            GameGridCoordinate Belegt(GameGridCoordinate z) { z.SpecialRule = CellSpecialRule.Stake; z.SpecialRuleAnnounced = true; return z; }

            var minispiel = Belegt(Zelle(0, new MinigameQuestion { Id = Guid.NewGuid() }));
            var leer = Belegt(Zelle(1));
            var gewoehnlich = Belegt(Zelle(2, Frage()));
            var nichtGeladen = Belegt(new GameGridCoordinate { Id = Guid.NewGuid(), QuestionBaseId = Guid.NewGuid() });

            Assert.IsTrue(Sonderregelraster.Bereinige(minispiel), "Regel auf dem Minispiel blieb.");
            Assert.IsTrue(Sonderregelraster.Bereinige(leer), "Regel auf der leeren Zelle blieb.");
            Assert.IsFalse(Sonderregelraster.Bereinige(gewoehnlich), "Regel auf einer gewoehnlichen Frage entfernt.");
            Assert.IsFalse(Sonderregelraster.Bereinige(nichtGeladen), "Ohne geladene Frage entschieden - das ist Sache des Spielstarts.");
            Assert.IsFalse(minispiel.SpecialRuleAnnounced || leer.SpecialRuleAnnounced, "Die Ankuendigung blieb stehen.");
        }

        [TestMethod]
        public void TheGameStartWarnsAboutRulesThatAreNotPlayed()
        {
            var minispielFrage = new MinigameQuestion { Id = Guid.NewGuid() };
            var minispiel = Zelle(0, minispielFrage);
            minispiel.SpecialRule = CellSpecialRule.Stake;
            var unbekannt = Zelle(1, Frage());
            unbekannt.SpecialRule = (CellSpecialRule)99;
            var gut = Zelle(2, Frage());
            gut.SpecialRule = CellSpecialRule.Risk;

            Assert.AreEqual(Sonderregelraster.MinigameRuleCode, Sonderregelraster.Startbefund(minispiel, minispielFrage)?.Code);
            Assert.AreEqual(Sonderregelraster.UnknownRuleCode, Sonderregelraster.Startbefund(unbekannt, unbekannt.QuestionBase)?.Code);
            Assert.IsNull(Sonderregelraster.Startbefund(gut, gut.QuestionBase));
            Assert.IsFalse(Sonderregelraster.Startbefund(minispiel, minispielFrage)!.IsError, "Eine nicht gespielte Regel haelt den Start an.");

            Assert.IsFalse(SpecialRuleProfiles.Applies(minispiel, minispielFrage), "Ein Minispiel spielt mit Sonderregel.");
            Assert.IsFalse(SpecialRuleProfiles.Applies(unbekannt, unbekannt.QuestionBase));
            Assert.IsTrue(SpecialRuleProfiles.Applies(gut, gut.QuestionBase));
        }
    }
}
