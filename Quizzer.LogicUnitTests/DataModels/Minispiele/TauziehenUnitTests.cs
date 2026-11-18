using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Teams;

namespace Quizzer.LogicUnitTests.DataModels.Minispiele
{
    /// <summary>
    /// Tauziehen als reine Funktion (M37 T9): die Plausibilität an der Serveruhr - höchstens 20 Tipps je Sekunde Laufzeit, nicht
    /// vor dem Start, nicht nach Dauer und Nachlauf, nie sinkend -, der Zug als Summe oder Durchschnitt, der Sieger, das Seil
    /// und die Tafel. Dazu das Profil: nur im Teamspiel, ohne Bestwert, Regeltext je Wertung.
    /// </summary>
    [TestClass]
    public class TauziehenUnitTests
    {
        private static readonly Guid Rot = new("00000000-0000-0000-0000-00000000000a");
        private static readonly Guid Blau = new("00000000-0000-0000-0000-00000000000b");
        private static readonly Guid Gelb = new("00000000-0000-0000-0000-00000000000c");

        private static TimeSpan Ms(int ms) => TimeSpan.FromMilliseconds(ms);

        private static TauziehenZug Zug(Guid team, int tipps, bool ende = false) => new(team, tipps, ende);

        // --- Die Grenze an der Serveruhr ---

        [TestMethod]
        public void AtMostTwentyTapsPerSecondOfServerTime()
        {
            Assert.IsTrue(TauziehenEvaluator.IstPlausibel(20, Ms(1000), 15));
            Assert.IsFalse(TauziehenEvaluator.IstPlausibel(21, Ms(1000), 15), "21 Tipps in einer Sekunde wurden angenommen.");
            Assert.IsTrue(TauziehenEvaluator.IstPlausibel(0, TimeSpan.Zero, 15));
            Assert.IsFalse(TauziehenEvaluator.IstPlausibel(1, TimeSpan.Zero, 15), "Ein Tipp ohne Laufzeit wurde angenommen.");
            Assert.IsFalse(TauziehenEvaluator.IstPlausibel(-1, Ms(1000), 15), "Ein negativer Stand wurde angenommen.");
            Assert.IsFalse(TauziehenEvaluator.IstPlausibel(0, Ms(-1), 15), "Ein Stand im Countdown wurde angenommen.");
        }

        /// <summary>Im Nachlauf zählt die Laufzeit nur bis zum Ablauf - mehr als Grenze mal Dauer gibt es nie.</summary>
        [TestMethod]
        public void TheLimitStopsAtTheEndOfTheDuration()
        {
            Assert.IsTrue(TauziehenEvaluator.IstPlausibel(100, Ms(6000), 5));
            Assert.IsFalse(TauziehenEvaluator.IstPlausibel(101, Ms(6000), 5), "Im Nachlauf durfte weiter getippt werden.");
        }

        [TestMethod]
        public void AReportCountsOnlyBetweenStartAndDeadline()
        {
            var neu = Zug(Rot, 10);

            Assert.IsNull(TauziehenEvaluator.Pruefe(null, neu, Ms(-1), 5), "Ein Stand im Countdown wurde angenommen.");
            Assert.AreEqual(neu, TauziehenEvaluator.Pruefe(null, neu, Ms(500), 5));
            Assert.AreEqual(neu, TauziehenEvaluator.Pruefe(null, neu, Ms(5000 + TauziehenEvaluator.NachlaufMs), 5),
                "Der Endstand, der im Nachlauf eintrifft, wurde verworfen.");
            Assert.IsNull(TauziehenEvaluator.Pruefe(null, neu, Ms(5001 + TauziehenEvaluator.NachlaufMs), 5),
                "Nach Dauer und Nachlauf wurde noch angenommen.");
            Assert.AreEqual(Ms(5000 + TauziehenEvaluator.NachlaufMs), TauziehenEvaluator.Frist(5));
        }

        [TestMethod]
        public void AReportNeverSinksAndNothingCountsAfterTheFinal()
        {
            var bisher = Zug(Rot, 10);

            Assert.IsNull(TauziehenEvaluator.Pruefe(bisher, Zug(Rot, 9), Ms(2000), 5), "Ein sinkender Stand wurde angenommen.");
            Assert.IsNotNull(TauziehenEvaluator.Pruefe(bisher, Zug(Rot, 10), Ms(2000), 5), "Ein gleicher Stand wurde verworfen.");
            Assert.IsNull(TauziehenEvaluator.Pruefe(Zug(Rot, 10, ende: true), Zug(Rot, 12), Ms(2000), 5), "Nach dem Endstand wurde noch angenommen.");
        }

        // --- Wer zieht stärker ---

        /// <summary>Rot hat mehr Tipps, aber zwei Mitglieder; Blau zieht allein - der Durchschnitt ist am Spiel die Vorgabe.</summary>
        [TestMethod]
        public void TheAverageMakesTheSizeOfTheTeamIrrelevant()
        {
            var zuege = new Dictionary<Guid, TauziehenZug>
            {
                [Guid.NewGuid()] = Zug(Rot, 10),
                [Guid.NewGuid()] = Zug(Rot, 0),
                [Guid.NewGuid()] = Zug(Blau, 6),
            };

            var schnitt = TauziehenEvaluator.Werte(zuege, TeamMinigameScoring.Average);
            CollectionAssert.AreEquivalent(new[] { Blau }, schnitt.Gewinner.ToList(), "Beim Durchschnitt gewinnt das größere Team.");
            Assert.AreEqual(5.0, schnitt.Teams.Single(t => t.TeamId == Rot).Zug, 1e-9, "Wer nie tippt, zählt im Durchschnitt nicht mit.");
            Assert.AreEqual("Ø 5,0 Tipps", schnitt.Teams.Single(t => t.TeamId == Rot).ZugText);

            var summe = TauziehenEvaluator.Werte(zuege, TeamMinigameScoring.Sum);
            CollectionAssert.AreEquivalent(new[] { Rot }, summe.Gewinner.ToList(), "Bei der Summe gewinnt nicht das Team mit den meisten Tipps.");
            Assert.AreEqual("10 Tipps", summe.Teams.Single(t => t.TeamId == Rot).ZugText);
        }

        /// <summary>Gleichstand ohne Rundung: 10 Tipps auf drei gegen 20 auf sechs - beide vorn.</summary>
        [TestMethod]
        public void AnExactTieLetsBothWinAndNoTapsWinNothing()
        {
            var zuege = new Dictionary<Guid, TauziehenZug>();
            foreach (var tipps in new[] { 4, 3, 3 })
                zuege[Guid.NewGuid()] = Zug(Rot, tipps);
            foreach (var tipps in new[] { 4, 4, 3, 3, 3, 3 })
                zuege[Guid.NewGuid()] = Zug(Blau, tipps);

            CollectionAssert.AreEquivalent(new[] { Rot, Blau }, TauziehenEvaluator.Werte(zuege, TeamMinigameScoring.Average).Gewinner.ToList());

            var leer = new Dictionary<Guid, TauziehenZug> { [Guid.NewGuid()] = Zug(Rot, 0), [Guid.NewGuid()] = Zug(Blau, 0) };
            Assert.AreEqual(0, TauziehenEvaluator.Werte(leer, TeamMinigameScoring.Average).Gewinner.Count, "Ohne einen Tipp hat ein Team gewonnen.");
        }

        [TestMethod]
        public void AnUnknownScoringPullsNothing()
        {
            var zuege = new Dictionary<Guid, TauziehenZug> { [Guid.NewGuid()] = Zug(Rot, 10), [Guid.NewGuid()] = Zug(Blau, 5) };

            Assert.AreEqual(0, TauziehenEvaluator.Werte(zuege, (TeamMinigameScoring)42).Gewinner.Count, "Eine unbekannte Wertung hat einen Sieger.");
        }

        /// <summary>R3-2: zieht nur ein Team (alle Gegner beim Start getrennt), gewinnt niemand - wie bei Staffel und Stellvertreter-Duell.</summary>
        [TestMethod]
        public void ATeamAloneWinsNothing()
        {
            var allein = new Dictionary<Guid, TauziehenZug> { [Guid.NewGuid()] = Zug(Rot, 30), [Guid.NewGuid()] = Zug(Rot, 12) };

            var ergebnis = TauziehenEvaluator.Werte(allein, TeamMinigameScoring.Sum);

            Assert.AreEqual(0, ergebnis.Gewinner.Count, "Ein Team allein hat das Tauziehen gewonnen.");
            Assert.AreEqual(42, ergebnis.Teams.Single().Tipps, "Das eine Team fehlt auf der Tafel.");
        }

        // --- Das Seil ---

        [TestMethod]
        public void TheRopeStandsBetweenTwoTeamsOnly()
        {
            Dictionary<Guid, TauziehenZug> Zwei(int rot, int blau) => new() { [Guid.NewGuid()] = Zug(Rot, rot), [Guid.NewGuid()] = Zug(Blau, blau) };

            Assert.AreEqual(-0.5, TauziehenEvaluator.Werte(Zwei(10, 5), TeamMinigameScoring.Sum).Seil!.Value, 1e-9, "Das Seil steht nicht auf halbem Weg zu Rot.");
            Assert.AreEqual(1.0, TauziehenEvaluator.Werte(Zwei(0, 7), TeamMinigameScoring.Sum).Seil!.Value, 1e-9);
            Assert.AreEqual(0.0, TauziehenEvaluator.Werte(Zwei(0, 0), TeamMinigameScoring.Sum).Seil!.Value, 1e-9);

            var drei = Zwei(1, 2);
            drei[Guid.NewGuid()] = Zug(Gelb, 3);
            Assert.IsNull(TauziehenEvaluator.Werte(drei, TeamMinigameScoring.Sum).Seil, "Bei drei Teams gibt es ein Seil.");
        }

        [TestMethod]
        public void TheBoardShowsEveryTeamAndTheWinnersOnlyAtTheEnd()
        {
            var zuege = new Dictionary<Guid, TauziehenZug>
            {
                [Guid.NewGuid()] = Zug(Rot, 10),
                [Guid.NewGuid()] = Zug(Blau, 20),
                [Guid.NewGuid()] = Zug(Blau, 0),
            };
            var ergebnis = TauziehenEvaluator.Werte(zuege, TeamMinigameScoring.Sum);
            string Name(Guid id) => id == Rot ? "✚ Rot" : "★ Blau";

            var laufend = TauziehenTafel.Zeilen(ergebnis, Name, ende: false);
            Assert.AreEqual("★ Blau", laufend[0].Name, "Das stärkere Team steht nicht oben.");
            Assert.AreEqual("20 Tipps (2 ziehen)", laufend[0].Zeit);
            Assert.AreEqual(0.5, laufend[1].BalkenAnteil, 1e-9);
            Assert.IsTrue(laufend.All(z => !z.Sieger), "Während der Runde ist schon ein Sieger markiert.");
            Assert.IsTrue(TauziehenTafel.Zeilen(ergebnis, Name, ende: true)[0].Sieger, "Am Ende ist der Sieger nicht markiert.");

            // Die Seite kommt aus dem Rang (SortOrder, V27), nicht aus der Kennung: Blau liegt nach Kennung hinter Rot.
            Func<Guid, int> Rang(params Guid[] reihe) => id => Array.IndexOf(reihe, id);
            var seil = TauziehenTafel.Seil(TauziehenEvaluator.Werte(zuege, TeamMinigameScoring.Sum, Rang(Rot, Blau)), Name)!;
            Assert.AreEqual(("✚ Rot", "★ Blau", "★ Blau zieht stärker"), (seil.Links, seil.Rechts, seil.Lage));
            Assert.AreEqual(0.5 * TauziehenSeil.HalbeLaenge, seil.Versatz, 1e-9);

            var umgekehrt = TauziehenTafel.Seil(TauziehenEvaluator.Werte(zuege, TeamMinigameScoring.Sum, Rang(Blau, Rot)), Name)!;
            Assert.AreEqual(("★ Blau", "✚ Rot", "★ Blau zieht stärker"), (umgekehrt.Links, umgekehrt.Rechts, umgekehrt.Lage), "Blau zuerst im Rang steht nicht links.");
            Assert.AreEqual(-0.5 * TauziehenSeil.HalbeLaenge, umgekehrt.Versatz, 1e-9);
        }

        // --- Das Profil ---

        // --- Die Karten ---

        /// <summary>Rot gewinnt: 100 auf drei als 34/33/33 in Sprecherfolge, Blau nichts; bei Gleichstand beide; nie Minus.</summary>
        [TestMethod]
        public void TheWinningTeamSplitsTheFieldValue()
        {
            var spiel = new Game { Id = Guid.NewGuid(), IsTeamGame = true };
            var rot = new GameTeam { Id = Rot, GameId = spiel.Id, Name = "Rot" };
            var blau = new GameTeam { Id = Blau, GameId = spiel.Id, Name = "Blau" };
            spiel.GameTeams.AddRange([rot, blau]);

            var karten = new[] { ("Anna", Rot), ("Bert", Rot), ("Cora", Rot), ("Dirk", Blau) }
                .Select(k => (Ergebnis: new QuestionResult { PlayerId = Guid.NewGuid(), GameTeamId = k.Item2 }, Name: k.Item1))
                .ToList();
            Guid Id(string name) => karten.Single(k => k.Name == name).Ergebnis.PlayerId;

            TauziehenErgebnis Sieger(params Guid[] teams) => new([], teams.ToHashSet());

            var vorschlaege = Teamwertung.TauziehenVorschlaege(spiel, karten, Sieger(Rot), 100);
            Assert.AreEqual((34, 33, 33), (vorschlaege[Id("Anna")].Punkte, vorschlaege[Id("Bert")].Punkte, vorschlaege[Id("Cora")].Punkte));
            Assert.IsFalse(vorschlaege.ContainsKey(Id("Dirk")), "Der Verlierer bekommt einen Vorschlag.");
            Assert.AreEqual("Rot gewinnt das Tauziehen: Anteil +34", vorschlaege[Id("Anna")].Grund);

            Assert.AreEqual(100, Teamwertung.TauziehenVorschlaege(spiel, karten, Sieger(Rot, Blau), 100)[Id("Dirk")].Punkte,
                "Bei Gleichstand bekommt Blau nichts.");
            Assert.AreEqual(0, Teamwertung.TauziehenVorschlaege(spiel, karten, Sieger(Rot), -100).Count, "Ein negativer Feldwert wird Minus.");
            Assert.AreEqual(0, Teamwertung.TauziehenVorschlaege(spiel, karten, Sieger(), 100).Count, "Ohne Sieger gibt es Vorschläge.");
        }

        [TestMethod]
        public void TheTugOfWarIsATeamKindWithoutARecord()
        {
            var profil = MinigameProfiles.For(MinigameKind.TugOfWar);

            Assert.AreEqual(MinispielTeamwertung.NurImTeamspiel, profil.TeamScoring);
            Assert.AreEqual(BuzzerControlsLayout.Minigame, profil.Layout);
            Assert.IsTrue(profil.ShowDurationSeconds && !profil.ShowRounds, "Das Tauziehen liest Runden oder keine Dauer.");
            Assert.IsNull(profil.Record);
            Assert.AreEqual(0, MinigameChecks.Content(Frage()).Count(), "Ein Tauziehen ohne Zeilen besteht die Prüfung nicht.");
        }

        private static MinigameQuestion Frage()
        {
            var frage = new MinigameQuestion { CategoryId = Guid.NewGuid() };
            MinigameProfiles.For(MinigameKind.TugOfWar).ApplyDefaults(frage);
            return frage;
        }

        /// <summary>Am Spiel: ohne Teamspiel ein Fehler, im Teamspiel braucht es zwei besetzte Teams - wer ohne Team ist, zählt nicht.</summary>
        [TestMethod]
        public void TheStartRejectsItWithoutTwoOccupiedTeams()
        {
            var spiel = new Game { Id = Guid.NewGuid(), IsTeamGame = false };
            var rot = new GameTeam { Id = Guid.NewGuid(), GameId = spiel.Id, Name = "Rot" };
            spiel.GameTeams.Add(rot);
            spiel.PlayerXGames.AddRange([new PlayerXGame { PlayerId = Guid.NewGuid(), GameTeamId = rot.Id }, new PlayerXGame { PlayerId = Guid.NewGuid() }]);

            Assert.AreEqual(MinigameChecks.OnlyInTeamGame, MinigameChecks.ForTeams(Frage(), spiel).Single().Code, "Ohne Teamspiel fällt das Tauziehen nicht auf.");

            spiel.IsTeamGame = true;
            Assert.AreEqual(MinigameChecks.NoOpponent, MinigameChecks.ForTeams(Frage(), spiel).Single().Code,
                "Ein Team und einer ohne Team gelten als zwei Seiten.");

            spiel.PlayerXGames[1].GameTeamId = Guid.NewGuid();
            var blau = new GameTeam { Id = spiel.PlayerXGames[1].GameTeamId!.Value, GameId = spiel.Id, Name = "Blau" };
            spiel.GameTeams.Add(blau);
            Assert.AreEqual(0, MinigameChecks.ForTeams(Frage(), spiel).Count(), "Zwei besetzte Teams wurden abgewiesen.");
        }

        [TestMethod]
        public void TheRulesTextNamesSumOrAverageInATeamGame()
        {
            var spiel = new Game { IsTeamGame = true, TeamMinigameScoring = TeamMinigameScoring.Average };

            StringAssert.Contains(MinigameProfiles.RulesFor(Frage(), spiel), "Im Teamspiel zieht der Durchschnitt eures Teams");
            spiel.TeamMinigameScoring = TeamMinigameScoring.Sum;
            StringAssert.Contains(MinigameProfiles.RulesFor(Frage(), spiel), "Im Teamspiel zieht die Summe eures Teams");
            Assert.IsFalse(MinigameProfiles.RulesFor(Frage(), spiel).Contains(MinigameProfiles.TeamValuePlaceholder), "Der Platzhalter steht im Text.");
            StringAssert.Contains(MinigameProfiles.RulesFor(Frage(), spiel), "15 Sekunden");
        }
    }
}
