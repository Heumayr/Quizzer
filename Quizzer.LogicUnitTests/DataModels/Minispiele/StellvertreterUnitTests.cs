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
    /// Das Stellvertreter-Duell als reine Funktion (M37 T10): die Disziplin aus Spielweise und Zahl der Teams, das Los nur aus
    /// den Kandidaten und der Zufallsquelle, die Wertung nur der Stellvertreter - ohne Gegner gewinnt niemand -, die Tafel und
    /// die Karten. Dazu das Profil und der Spielstart.
    /// </summary>
    [TestClass]
    public class StellvertreterUnitTests
    {
        private static readonly Guid Rot = new("00000000-0000-0000-0000-00000000000a");
        private static readonly Guid Blau = new("00000000-0000-0000-0000-00000000000b");
        private static readonly Guid Gelb = new("00000000-0000-0000-0000-00000000000c");

        private static readonly Guid Anna = new("00000000-0000-0000-0000-000000000001");
        private static readonly Guid Bert = new("00000000-0000-0000-0000-000000000002");
        private static readonly Guid Carl = new("00000000-0000-0000-0000-000000000003");
        private static readonly Guid Dora = new("00000000-0000-0000-0000-000000000004");

        private static ReactionAttempt Ms(int ms) => new(ReactionOutcome.Valid, ms);

        private static IReadOnlyDictionary<Guid, ReactionAttempt> Runde(params (Guid Wer, ReactionAttempt Versuch)[] versuche)
            => versuche.ToDictionary(v => v.Wer, v => v.Versuch);

        private static readonly Stellvertreter[] Zwei = [new(Rot, Anna, false), new(Blau, Carl, false)];

        [TestMethod]
        public void RockPaperScissorsOnlyWithExactlyTwoTeams()
        {
            Assert.AreEqual(DuellEvaluator.SchereSteinPapierVariant, StellvertreterEvaluator.Disziplin(StellvertreterEvaluator.SchereSteinPapierVariant, 2));
            Assert.AreEqual(DuellEvaluator.ReaktionVariant, StellvertreterEvaluator.Disziplin(StellvertreterEvaluator.SchereSteinPapierVariant, 3),
                "Drei Teams spielen Schere, Stein, Papier.");
            Assert.AreEqual(DuellEvaluator.ReaktionVariant, StellvertreterEvaluator.Disziplin(StellvertreterEvaluator.SchereSteinPapierVariant, 1));
            Assert.AreEqual(DuellEvaluator.ReaktionVariant, StellvertreterEvaluator.Disziplin(StellvertreterEvaluator.ReaktionVariant, 2));
            Assert.AreEqual(DuellEvaluator.ReaktionVariant, StellvertreterEvaluator.Disziplin(7, 2), "Eine unbekannte Spielweise spielt nicht die Vorgabe.");
        }

        /// <summary>Gelost wird über die Kandidaten nach Kennung - die Reihenfolge, in der sie hereinkommen, ändert nichts.</summary>
        [TestMethod]
        public void TheLotPicksOnlyACandidateByTheServersRandomSource()
        {
            Assert.AreEqual(Anna, StellvertreterEvaluator.Lose([Bert, Anna], (_, _) => 0));
            Assert.AreEqual(Bert, StellvertreterEvaluator.Lose([Bert, Anna], (_, _) => 1));
            Assert.AreEqual(Bert, StellvertreterEvaluator.Lose([Anna, Bert], (_, _) => 1), "Die Reihenfolge der Kandidaten lenkt das Los.");
            Assert.AreEqual(Bert, StellvertreterEvaluator.Lose([Anna, Bert], (_, _) => 99), "Eine Zahl außerhalb trifft keinen Kandidaten.");
            Assert.IsNull(StellvertreterEvaluator.Lose([], (_, _) => 0));

            var grenzen = new List<(int, int)>();
            StellvertreterEvaluator.Lose([Anna, Bert, Carl, Anna], (a, b) => { grenzen.Add((a, b)); return 0; });
            Assert.AreEqual((0, 3), grenzen.Single(), "Gelost wird nicht genau unter den drei Kandidaten.");
        }

        [TestMethod]
        public void TheFastestProxyWinsAndOthersDoNotCount()
        {
            var ergebnis = StellvertreterEvaluator.WerteReaktion(Zwei, [
                Runde((Anna, Ms(300)), (Carl, Ms(250)), (Bert, Ms(100))),
                Runde((Anna, Ms(200)), (Carl, new ReactionAttempt(ReactionOutcome.FalseStart, null))),
            ]);

            CollectionAssert.AreEquivalent(new[] { Anna }, ergebnis.Sieger.ToList(), "Bert ist kein Stellvertreter und gewann trotzdem.");
            Assert.IsTrue(ergebnis.IstSiegerteam(Rot) && !ergebnis.IstSiegerteam(Blau));
            Assert.AreEqual(("200 ms", "250 ms"), (ergebnis.Leistung(Anna), ergebnis.Leistung(Carl)));
            Assert.IsNull(ergebnis.Reaktion!.BestzeitVon(Bert), "Bert steht mit einer Zeit in der Wertung.");
            Assert.IsFalse(StellvertreterEvaluator.IstEntschieden(ergebnis, 1), "Die Reaktion endet vor der letzten Runde.");
        }

        /// <summary>Gleiche Bestzeit: alle vorn. Ein Team allein hat keinen Gegner - sonst bekäme es die Punkte geschenkt.</summary>
        [TestMethod]
        public void ATieLetsAllWinAndNoOpponentLetsNobodyWin()
        {
            Stellvertreter[] drei = [.. Zwei, new(Gelb, Dora, true)];
            var gleich = StellvertreterEvaluator.WerteReaktion(drei, [Runde((Anna, Ms(200)), (Carl, Ms(200)), (Dora, Ms(400)))]);
            CollectionAssert.AreEquivalent(new[] { Anna, Carl }, gleich.Sieger.ToList());

            var allein = StellvertreterEvaluator.WerteReaktion([new(Rot, Anna, false)], [Runde((Anna, Ms(200)))]);
            Assert.AreEqual(0, allein.Sieger.Count, "Ein Team ohne Gegner hat gewonnen.");

            var nichts = StellvertreterEvaluator.WerteReaktion(Zwei, [Runde()]);
            Assert.AreEqual(0, nichts.Sieger.Count);
            Assert.AreEqual("keine Abgabe", nichts.Leistung(Anna));
        }

        [TestMethod]
        public void RockPaperScissorsIsBestOfNBetweenTheTwoProxies()
        {
            var zuege = new[]
            {
                new Dictionary<Guid, SchereSteinPapier> { [Anna] = SchereSteinPapier.Stein, [Carl] = SchereSteinPapier.Schere },
                new Dictionary<Guid, SchereSteinPapier> { [Anna] = SchereSteinPapier.Papier, [Carl] = SchereSteinPapier.Stein, [Bert] = SchereSteinPapier.Schere },
            };

            var ergebnis = StellvertreterEvaluator.WerteSchereSteinPapier(Zwei, zuege);
            CollectionAssert.AreEquivalent(new[] { Anna }, ergebnis.Sieger.ToList());
            Assert.AreEqual("2 Siege", ergebnis.Leistung(Anna));
            Assert.IsTrue(StellvertreterEvaluator.IstEntschieden(ergebnis, 3), "2:0 bei Best of 3 ist nicht entschieden.");
            Assert.IsFalse(StellvertreterEvaluator.IstEntschieden(StellvertreterEvaluator.WerteSchereSteinPapier(Zwei, zuege.Take(1)), 3));

            Stellvertreter[] drei = [.. Zwei, new(Gelb, Dora, false)];
            Assert.AreEqual(0, StellvertreterEvaluator.WerteSchereSteinPapier(drei, zuege).Sieger.Count, "Drei Stellvertreter spielten Schere, Stein, Papier.");
        }

        // --- Tafel ---

        [TestMethod]
        public void TheBoardShowsWhoStillChoosesAndMarksTheWinnerOnlyAtTheEnd()
        {
            string Team(Guid id) => id == Rot ? "✚ Rot" : id == Blau ? "★ Blau" : "▲ Gelb";
            string Name(Guid id) => id == Anna ? "Anna" : id == Carl ? "Carl" : "Dora";

            var wahl = StellvertreterTafel.Wahl([Rot, Blau], [new(Blau, Carl, true)], Team, Name);
            Assert.AreEqual("wählt …", wahl.Single(z => z.Name == "✚ Rot").Zeit);
            Assert.AreEqual("Carl (gelost)", wahl.Single(z => z.Name == "★ Blau").Zeit);

            var ergebnis = StellvertreterEvaluator.WerteReaktion(Zwei, [Runde((Anna, Ms(300)), (Carl, Ms(250)))]);
            var runde = StellvertreterTafel.Runde(ergebnis, new Dictionary<Guid, object> { [Anna] = Ms(300), [Carl] = Ms(250) }, Team, Name);
            Assert.IsTrue(runde.All(z => !z.Sieger), "Nach einer Reaktionsrunde ist schon ein Sieger markiert.");
            Assert.AreEqual("★ Blau – Carl", runde.Single(z => z.Zeit == "250 ms").Name);

            var laufend = StellvertreterTafel.Stand(ergebnis, Team, Name, ende: false);
            Assert.IsTrue(laufend.All(z => !z.Sieger), "Vor dem Ende ist ein Sieger markiert.");

            var ende = StellvertreterTafel.Stand(ergebnis, Team, Name, ende: true);
            Assert.AreEqual(("★ Blau – Carl", "250 ms", "★ Sieger"), (ende[0].Name, ende[0].Zeit, ende[0].SiegerText));
        }

        [TestMethod]
        public void ARockPaperScissorsRoundMarksItsWinner()
        {
            var ergebnis = StellvertreterEvaluator.WerteSchereSteinPapier(Zwei, []);
            var runde = StellvertreterTafel.Runde(ergebnis,
                new Dictionary<Guid, object> { [Anna] = SchereSteinPapier.Papier, [Carl] = SchereSteinPapier.Stein }, _ => "T", _ => "N");

            Assert.AreEqual(1, runde.Count(z => z.Sieger));
            Assert.AreEqual("Papier", runde.Single(z => z.Sieger).Zeit);
        }

        // --- Karten ---

        /// <summary>Das Team des Siegers bekommt den Feldwert einmal, aufgeteilt; die anderen nichts, nie Minus.</summary>
        [TestMethod]
        public void TheWinnersTeamSplitsTheFieldValue()
        {
            var spiel = new Game { Id = Guid.NewGuid(), IsTeamGame = true };
            spiel.GameTeams.AddRange([new GameTeam { Id = Rot, GameId = spiel.Id, Name = "Rot" }, new GameTeam { Id = Blau, GameId = spiel.Id, Name = "Blau" }]);

            var karten = new[] { (Anna, Rot), (Bert, Rot), (Dora, Rot), (Carl, Blau) }
                .Select(k => (Ergebnis: new QuestionResult { PlayerId = k.Item1, GameTeamId = k.Item2 }, Name: k.Item1.ToString()))
                .ToList();

            var ergebnis = StellvertreterEvaluator.WerteReaktion(Zwei, [Runde((Anna, Ms(200)), (Carl, Ms(300)))]);
            var vorschlaege = Teamwertung.StellvertreterVorschlaege(spiel, karten, ergebnis, 100);

            Assert.AreEqual(100, vorschlaege.Values.Sum(v => v.Punkte), "Rot bekommt nicht genau den Feldwert.");
            Assert.AreEqual(3, vorschlaege.Count, "Nicht jedes Mitglied von Rot hat seinen Anteil.");
            Assert.IsFalse(vorschlaege.ContainsKey(Carl), "Blau bekommt einen Vorschlag.");
            Assert.AreEqual("Rot gewinnt das Stellvertreter-Duell: Anteil +33", vorschlaege[Bert].Grund);

            Assert.AreEqual(0, Teamwertung.StellvertreterVorschlaege(spiel, karten, ergebnis, -100).Count, "Ein negativer Feldwert wird Minus.");
            var ohne = StellvertreterEvaluator.WerteReaktion(Zwei, [Runde()]);
            Assert.AreEqual(0, Teamwertung.StellvertreterVorschlaege(spiel, karten, ohne, 100).Count, "Ohne Sieger gibt es Vorschläge.");
        }

        // --- Profil und Spielstart ---

        private static MinigameQuestion Frage(int variante = StellvertreterEvaluator.ReaktionVariant)
        {
            var frage = new MinigameQuestion { CategoryId = Guid.NewGuid() };
            MinigameProfiles.For(MinigameKind.ProxyDuel).ApplyDefaults(frage);
            frage.Variant = variante;
            return frage;
        }

        [TestMethod]
        public void TheProxyDuelIsATeamKindWithRoundsAndWithoutARecord()
        {
            var profil = MinigameProfiles.For(MinigameKind.ProxyDuel);

            Assert.AreEqual(MinispielTeamwertung.NurImTeamspiel, profil.TeamScoring);
            Assert.AreEqual(BuzzerControlsLayout.Minigame, profil.Layout);
            Assert.IsTrue(profil.ShowRounds && !profil.ShowDurationSeconds && !profil.ShowShare);
            Assert.IsNull(profil.Record);
            Assert.AreEqual("Reaktion", profil.Variants[StellvertreterEvaluator.ReaktionVariant].Name);
            StringAssert.StartsWith(profil.Variants.ElementAtOrDefault(profil.TwoTeamsOnlyVariant ?? -1)?.Name, "Schere, Stein, Papier",
                "Nur für zwei Teams steht nicht Schere, Stein, Papier.");
            Assert.AreEqual(0, MinigameChecks.Content(Frage()).Count(), "Ein Stellvertreter-Duell ohne Zeilen besteht die Prüfung nicht.");
            StringAssert.Contains(MinigameProfiles.RulesFor(Frage(StellvertreterEvaluator.SchereSteinPapierVariant)), "Best of 3");
        }

        /// <summary>Ohne Teamspiel ein Fehler; Schere, Stein, Papier mit drei Teams eine Anmerkung - mit zwei nicht.</summary>
        [TestMethod]
        public void TheStartNotesRockPaperScissorsWithMoreThanTwoTeams()
        {
            var spiel = new Game { Id = Guid.NewGuid(), IsTeamGame = false };
            foreach (var team in new[] { Rot, Blau, Gelb })
            {
                spiel.GameTeams.Add(new GameTeam { Id = team, GameId = spiel.Id, Name = team.ToString() });
                spiel.PlayerXGames.Add(new PlayerXGame { PlayerId = Guid.NewGuid(), GameTeamId = team });
            }

            Assert.AreEqual(MinigameChecks.OnlyInTeamGame, MinigameChecks.ForTeams(Frage(), spiel).Single().Code);

            spiel.IsTeamGame = true;
            Assert.AreEqual(0, MinigameChecks.ForTeams(Frage(), spiel).Count(), "Die Reaktion mit drei Teams fällt auf.");

            var befund = MinigameChecks.ForTeams(Frage(StellvertreterEvaluator.SchereSteinPapierVariant), spiel).Single();
            Assert.AreEqual((MinigameChecks.TwoTeamsOnly, ValidationSeverity.Warning), (befund.Code, befund.Severity));
            StringAssert.Contains(befund.Message, "gespielt wird „Reaktion“");

            spiel.PlayerXGames.RemoveAt(2);
            Assert.AreEqual(0, MinigameChecks.ForTeams(Frage(StellvertreterEvaluator.SchereSteinPapierVariant), spiel).Count(),
                "Schere, Stein, Papier mit zwei Teams fällt auf.");
        }

        [TestMethod]
        public void TheRulesTextSaysTheWinnersTeamShares()
        {
            var spiel = new Game { IsTeamGame = true };

            StringAssert.Contains(MinigameProfiles.RulesFor(Frage(), spiel), "Im Teamspiel holt der Sieger die Punkte für sein Team, aufgeteilt auf alle.");
            StringAssert.Contains(MinigameProfiles.RulesFor(Frage(), spiel), "aus 3 Runden");
        }
    }
}
