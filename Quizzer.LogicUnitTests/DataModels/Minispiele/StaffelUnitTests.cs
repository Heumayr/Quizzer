using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Teams;

namespace Quizzer.LogicUnitTests.DataModels.Minispiele
{
    /// <summary>
    /// Die Staffel als reine Funktion (M37 T11): wer läuft, wohin ein Fehler weitergibt, was mit einem Teil geschieht, den alle
    /// verfehlen, die Eingriffe der Spielleitung, die Wertung - ohne Gegner gewinnt niemand -, die Tafel ohne Lösung vor dem Ende und
    /// die Karten. Dazu das Profil und der Spielstart.
    /// </summary>
    [TestClass]
    public class StaffelUnitTests
    {
        private static readonly Guid Rot = new("00000000-0000-0000-0000-00000000000a");
        private static readonly Guid Blau = new("00000000-0000-0000-0000-00000000000b");
        private static readonly Guid Gelb = new("00000000-0000-0000-0000-00000000000c");

        private static readonly Guid Anna = new("00000000-0000-0000-0000-000000000001");
        private static readonly Guid Bert = new("00000000-0000-0000-0000-000000000002");
        private static readonly Guid Carl = new("00000000-0000-0000-0000-000000000003");
        private static readonly Guid Dora = new("00000000-0000-0000-0000-000000000004");

        /// <summary>Rot läuft mit Anna und Bert, Blau mit Carl allein.</summary>
        private static StaffelStand Beginne(int teile = 4, Guid? start = null, Func<Guid, bool>? erreichbar = null)
            => StaffelEvaluator.Beginne([new StaffelTeam(Rot, [Anna, Bert]), new StaffelTeam(Blau, [Carl])], teile, start ?? Rot, erreichbar);

        [TestMethod]
        public void TheRunnerAnswersAndTheNextMemberOfHisTeamRunsTheNextPart()
        {
            var stand = Beginne();
            Assert.AreEqual((Rot, Anna, 0, 1), (stand.TeamAmZug, stand.Laeufer, stand.Teil, stand.Zug));

            stand = StaffelEvaluator.Antworte(stand, richtig: true);

            Assert.AreEqual((Rot, Bert, 1, 2), (stand.TeamAmZug, stand.Laeufer, stand.Teil, stand.Zug), "Nach einem Treffer läuft nicht Bert den nächsten Teil.");
            Assert.AreEqual(Rot, stand.Geloest[0]);
            Assert.AreEqual(new StaffelVersuch(1, Rot, Anna, true), stand.Letzter);

            stand = StaffelEvaluator.Antworte(stand, richtig: true);
            Assert.AreEqual(Anna, stand.Laeufer, "Nach Bert ist Anna wieder dran.");
        }

        [TestMethod]
        public void AMissPassesThePartToTheNextTeam()
        {
            var stand = StaffelEvaluator.Antworte(Beginne(), richtig: false);

            Assert.AreEqual((Blau, Carl, 0), (stand.TeamAmZug, stand.Laeufer, stand.Teil), "Der Fehler gibt den Teil nicht an Blau weiter.");
            CollectionAssert.AreEqual(new[] { Rot }, stand.Gescheitert.ToList());
            Assert.IsNull(stand.Geloest[0]);

            stand = StaffelEvaluator.Antworte(stand, richtig: true);

            Assert.AreEqual((Blau, Carl, 1), (stand.TeamAmZug, stand.Laeufer, stand.Teil), "Blau läuft nach dem Treffer nicht weiter.");
            Assert.AreEqual(Blau, stand.Geloest[0]);
            Assert.AreEqual(0, stand.Gescheitert.Count, "Der nächste Teil beginnt mit gescheiterten Teams.");

            // Rot kommt erst nach einem Fehler von Blau wieder dran - mit Bert, Anna hat schon.
            stand = StaffelEvaluator.Antworte(stand, richtig: false);
            Assert.AreEqual((Rot, Bert), (stand.TeamAmZug, stand.Laeufer));
        }

        /// <summary>Drei Teams: ein Team, das den Teil schon verfehlt hat, bekommt ihn nicht noch einmal.</summary>
        [TestMethod]
        public void ATeamThatMissedThePartDoesNotGetItAgain()
        {
            var stand = StaffelEvaluator.Beginne(
                [new StaffelTeam(Rot, [Anna]), new StaffelTeam(Blau, [Carl]), new StaffelTeam(Gelb, [Dora])], 3, Blau);

            stand = StaffelEvaluator.Antworte(stand, richtig: false);
            Assert.AreEqual(Gelb, stand.TeamAmZug, "Nach Blau ist Gelb dran.");

            stand = StaffelEvaluator.Antworte(stand, richtig: false);
            Assert.AreEqual((Rot, 0), (stand.TeamAmZug, stand.Teil), "Rot hat den Teil noch nicht versucht.");

            stand = StaffelEvaluator.Antworte(stand, richtig: false);
            Assert.AreEqual((Blau, 1), (stand.TeamAmZug, stand.Teil), "Haben alle verfehlt, beginnt das Team nach Rot den nächsten Teil.");
            Assert.IsNull(stand.Geloest[0], "Ein von allen verfehlter Teil gehört jemandem.");
        }

        [TestMethod]
        public void WhenAllTeamsMissThePartStaysOpenAndTheTeamAfterTheLastStartsTheNext()
        {
            var stand = StaffelEvaluator.Antworte(StaffelEvaluator.Antworte(Beginne(), richtig: false), richtig: false);

            Assert.AreEqual((Rot, Bert, 1), (stand.TeamAmZug, stand.Laeufer, stand.Teil), "Nach Carls Fehler beginnt nicht Rot mit Bert den nächsten Teil.");
            Assert.IsNull(stand.Geloest[0]);
            Assert.AreEqual(0, stand.Gescheitert.Count);
            Assert.AreEqual(3, stand.Zug);
        }

        [TestMethod]
        public void TheTriggersTeamStartsAndTeamsWithoutRunnersDoNotRun()
        {
            var stand = StaffelEvaluator.Beginne(
                [new StaffelTeam(Rot, [Anna]), new StaffelTeam(Gelb, []), new StaffelTeam(Blau, [Carl])], 3, Blau);

            Assert.AreEqual(Blau, stand.TeamAmZug, "Es beginnt nicht das Team des Auslösers.");
            CollectionAssert.AreEqual(new[] { Rot, Blau }, stand.Teams.Select(t => t.TeamId).ToList(), "Ein Team ohne Läufer läuft mit.");

            Assert.AreEqual(Rot, StaffelEvaluator.Beginne([new StaffelTeam(Rot, [Anna]), new StaffelTeam(Blau, [Carl])], 3, Gelb).TeamAmZug,
                "Ohne Team des Auslösers beginnt nicht das erste.");

            var leer = StaffelEvaluator.Beginne([new StaffelTeam(Rot, [])], 3);
            Assert.IsTrue(leer.Fertig && leer.Laeufer == null, "Ohne Läufer läuft eine Staffel.");
            Assert.AreSame(leer, StaffelEvaluator.Antworte(leer, true), "Eine Staffel ohne Läufer nimmt eine Antwort an.");
        }

        [TestMethod]
        public void AnUnreachableRunnerIsSkippedWhenHisTeamComesUp()
        {
            var getrennt = new HashSet<Guid> { Bert };
            var stand = Beginne(erreichbar: id => !getrennt.Contains(id));

            stand = StaffelEvaluator.Antworte(stand, richtig: true, id => !getrennt.Contains(id));
            Assert.AreEqual(Anna, stand.Laeufer, "Der getrennte Bert läuft.");

            // Ist keiner erreichbar, bleibt es beim Platz - die Spielleitung gibt weiter.
            getrennt.Add(Anna);
            stand = StaffelEvaluator.Antworte(stand, richtig: true, id => !getrennt.Contains(id));
            Assert.AreEqual(Bert, stand.Laeufer);
        }

        [TestMethod]
        public void TheLastPartFinishesTheRelay()
        {
            var stand = Beginne(teile: 3);

            for (var i = 0; i < 3; i++)
                stand = StaffelEvaluator.Antworte(stand, richtig: true);

            Assert.IsTrue(stand.Fertig, "Nach drei Teilen ist die Staffel nicht durch.");
            Assert.IsNull(stand.Laeufer);
            Assert.IsNull(stand.TeamAmZug);
            Assert.AreSame(stand, StaffelEvaluator.Antworte(stand, true), "Nach dem letzten Teil zählt noch eine Antwort.");
        }

        // --- Eingriffe der Spielleitung ---

        [TestMethod]
        public void TheGameMasterAcceptsTheLastMissOnce()
        {
            var falsch = StaffelEvaluator.Antworte(Beginne(), richtig: false);
            var gegolten = StaffelEvaluator.LasseGelten(falsch);

            Assert.IsNotNull(gegolten, "Die abgelehnte Antwort lässt sich nicht gelten lassen.");
            Assert.AreEqual((Rot, Bert, 1), (gegolten.TeamAmZug, gegolten.Laeufer, gegolten.Teil), "Rot läuft danach nicht mit Bert weiter.");
            Assert.AreEqual(Rot, gegolten.Geloest[0]);
            Assert.AreEqual(new StaffelVersuch(1, Rot, Anna, true, Gegolten: true), gegolten.Letzter);
            Assert.AreEqual(1, gegolten.Verlauf.Count, "Der abgelehnte Versuch steht noch im Verlauf.");
            Assert.AreEqual(0, gegolten.Gescheitert.Count);
            Assert.IsTrue(gegolten.Zug > falsch.Zug, "Der Zug steigt nicht - ein Telefon aus der Zeit nach dem Fehler träfe noch.");

            Assert.IsNull(StaffelEvaluator.LasseGelten(gegolten), "Zweimal gelten lassen geht.");
            Assert.IsNull(StaffelEvaluator.LasseGelten(StaffelEvaluator.Antworte(Beginne(), richtig: true)), "Ein Treffer lässt sich gelten lassen.");
            Assert.IsNull(StaffelEvaluator.LasseGelten(StaffelEvaluator.Antworte(falsch, richtig: true)), "Nach dem nächsten Treffer gilt der Fehler davor noch.");
            Assert.AreEqual(Blau, StaffelEvaluator.LasseGelten(StaffelEvaluator.Antworte(falsch, richtig: false))?.Geloest[0],
                "Nach zwei Fehlern gilt nicht der letzte.");
            Assert.IsNull(StaffelEvaluator.LasseGelten(StaffelEvaluator.GibWeiter(Beginne())), "Ein Weitergeben lässt sich gelten lassen.");
        }

        [TestMethod]
        public void TheGameMasterPassesForTheTeamOnTurn()
        {
            var stand = StaffelEvaluator.GibWeiter(Beginne());

            Assert.AreEqual((Blau, Carl, 0), (stand.TeamAmZug, stand.Laeufer, stand.Teil));
            Assert.AreEqual(new StaffelVersuch(1, Rot, Anna, false, Gepasst: true), stand.Letzter);
            Assert.IsNull(stand.VorDemFehler);
            Assert.AreEqual(2, stand.Zug);
        }

        // --- Wertung ---

        [TestMethod]
        public void TheTeamWithTheMostPartsWins()
        {
            // Rot löst Teil 1 und 2, verfehlt 3; Blau löst 3, verfehlt 4; Rot löst 4.
            var stand = Beginne();
            foreach (var richtig in new[] { true, true, false, true, false, true })
                stand = StaffelEvaluator.Antworte(stand, richtig);

            var ergebnis = StaffelEvaluator.Werte(stand);
            Assert.IsTrue(stand.Fertig);
            CollectionAssert.AreEqual(new[] { new StaffelTeamwert(Rot, 3), new StaffelTeamwert(Blau, 1) }, ergebnis.Teams.ToList());
            CollectionAssert.AreEqual(new[] { Rot }, ergebnis.Sieger.ToList());

            // Gleichstand: beide vorn.
            var gleich = StaffelEvaluator.Antworte(StaffelEvaluator.Antworte(StaffelEvaluator.Antworte(Beginne(teile: 2), true), false), true);
            CollectionAssert.AreEquivalent(new[] { Rot, Blau }, StaffelEvaluator.Werte(gleich).Sieger.ToList(), "Bei Gleichstand gewinnen nicht beide.");

            // Niemand hat einen Teil: kein Sieger.
            var keiner = StaffelEvaluator.Antworte(StaffelEvaluator.Antworte(Beginne(teile: 1), false), false);
            Assert.AreEqual(0, StaffelEvaluator.Werte(keiner).Sieger.Count, "Ohne einen Teil gewinnt ein Team.");
        }

        [TestMethod]
        public void WithoutAnOpponentNobodyWins()
        {
            var allein = StaffelEvaluator.Antworte(StaffelEvaluator.Beginne([new StaffelTeam(Rot, [Anna])], 3), richtig: true);

            Assert.AreEqual(1, allein.GeloestVon(Rot));
            Assert.AreEqual(0, StaffelEvaluator.Werte(allein).Sieger.Count, "Ein Team ohne Gegner gewinnt die Staffel.");
        }

        // --- Tafel und Karten ---

        private static readonly IReadOnlyList<MinispielZeile> Planeten = [new("Merkur", false), new("Venus", false), new("Erde", false), new("Mars", false)];

        private static string TeamName(Guid id) => id == Rot ? "✚ Rot" : "★ Blau";

        private static string Name(Guid id) => id == Anna ? "Anna" : id == Bert ? "Bert" : "Carl";

        [TestMethod]
        public void TheBoardShowsAMissedPartsSolutionOnlyAtTheEnd()
        {
            // Rot löst Merkur; Venus verfehlen beide; Anna ist mit der Erde dran.
            var stand = StaffelEvaluator.Antworte(StaffelEvaluator.Antworte(StaffelEvaluator.Antworte(Beginne(), true), false), false);

            var laufend = StaffelTafel.Gespielt(stand, Planeten, TeamName, ende: false);
            Assert.AreEqual(2, laufend.Count, "Während der Runde stehen nicht genau die gespielten Teile da.");
            Assert.AreEqual(("Teil 1: Merkur", "✚ Rot"), (laufend[0].Name, laufend[0].Zeit));
            Assert.AreEqual(("Teil 2", "verfehlt"), (laufend[1].Name, laufend[1].Zeit), "Der verfehlte Teil zeigt seine Lösung vor dem Ende.");
            Assert.IsFalse(laufend.Any(z => z.Name.Contains("Venus") || z.Name.Contains("Erde") || z.Name.Contains("Mars")), "Eine offene Lösung steht am Beamer.");

            var teams = StaffelTafel.Teams(stand, TeamName, Name, ende: false);
            Assert.AreEqual(("✚ Rot", "1 Teil – Anna läuft", false), (teams[0].Name, teams[0].Zeit, teams[0].Sieger));
            Assert.AreEqual(("★ Blau", "0 Teile"), (teams[1].Name, teams[1].Zeit));
            Assert.AreEqual(0.25, teams[0].Balken);

            var ende = StaffelTafel.Gespielt(stand, Planeten, TeamName, ende: true);
            CollectionAssert.AreEqual(new[] { "Teil 1: Merkur", "Teil 2: Venus", "Teil 3: Erde", "Teil 4: Mars" }, ende.Select(z => z.Name).ToList());
            Assert.AreEqual("verfehlt", ende[3].Zeit);
            Assert.IsTrue(StaffelTafel.Teams(stand, TeamName, Name, ende: true)[0].Sieger, "Am Ende ist Rot nicht markiert.");
            Assert.IsFalse(StaffelTafel.Teams(stand, TeamName, Name, ende: true)[0].Zeit.Contains("läuft"), "Am Ende läuft noch jemand.");
        }

        /// <summary>Das Siegerteam bekommt den Feldwert einmal, aufgeteilt; die anderen nichts, nie Minus.</summary>
        [TestMethod]
        public void TheWinnersTeamSplitsTheFieldValue()
        {
            var spiel = new Game { Id = Guid.NewGuid(), IsTeamGame = true };
            spiel.GameTeams.AddRange([new GameTeam { Id = Rot, GameId = spiel.Id, Name = "Rot" }, new GameTeam { Id = Blau, GameId = spiel.Id, Name = "Blau" }]);

            var karten = new[] { (Anna, Rot), (Bert, Rot), (Dora, Rot), (Carl, Blau) }
                .Select(k => (Ergebnis: new QuestionResult { PlayerId = k.Item1, GameTeamId = k.Item2 }, Name: k.Item1.ToString()))
                .ToList();

            var ergebnis = new StaffelErgebnis([new StaffelTeamwert(Rot, 2), new StaffelTeamwert(Blau, 1)], [Rot]);
            var vorschlaege = Teamwertung.StaffelVorschlaege(spiel, karten, ergebnis, 100);

            Assert.AreEqual(100, vorschlaege.Values.Sum(v => v.Punkte), "Rot bekommt nicht genau den Feldwert.");
            Assert.AreEqual(3, vorschlaege.Count, "Nicht jedes Mitglied von Rot hat seinen Anteil.");
            Assert.IsFalse(vorschlaege.ContainsKey(Carl), "Blau bekommt einen Vorschlag.");
            Assert.AreEqual("Rot gewinnt die Staffel: Anteil +33", vorschlaege[Bert].Grund);

            Assert.AreEqual(0, Teamwertung.StaffelVorschlaege(spiel, karten, ergebnis, -100).Count, "Ein negativer Feldwert wird Minus.");
            Assert.AreEqual(0, Teamwertung.StaffelVorschlaege(spiel, karten, StaffelErgebnis.Leer, 100).Count, "Ohne Sieger gibt es Vorschläge.");
        }

        // --- Profil und Spielstart ---

        private static MinigameQuestion Frage(params string[] teile)
        {
            var frage = new MinigameQuestion { CategoryId = Guid.NewGuid() };
            MinigameProfiles.For(MinigameKind.Relay).ApplyDefaults(frage);

            for (var i = 0; i < teile.Length; i++)
                frage.Steps.Add(new QuestionStepResource { Id = Guid.NewGuid(), QuestionBaseId = frage.Id, SequenceNumber = (i + 1) * 10, StepText = teile[i] });

            return frage;
        }

        [TestMethod]
        public void TheRelayIsATeamKindWithPartsAndWithoutARecord()
        {
            var profil = MinigameProfiles.For(MinigameKind.Relay);

            Assert.AreEqual(MinispielTeamwertung.NurImTeamspiel, profil.TeamScoring);
            Assert.AreEqual(BuzzerControlsLayout.Minigame, profil.Layout);
            Assert.IsFalse(profil.ShowRounds || profil.ShowDurationSeconds || profil.ShowShare || profil.ShowTruePerRow);
            Assert.IsNull(profil.Record);
            Assert.AreEqual(3, profil.MinOptions, "Die Staffel braucht nicht mindestens drei Teile.");

            Assert.AreEqual(0, MinigameChecks.Content(Frage("Merkur", "Venus", "Erde")).Count(), "Drei Teile bestehen die Prüfung nicht.");
            Assert.AreEqual(MinigameChecks.TooFewOptions, MinigameChecks.Content(Frage("Merkur", "Venus", "  ")).Single().Code,
                "Eine leere Zeile zählt als Teil - im Spiel zählt sie nicht.");
        }

        [TestMethod]
        public void TheStartReportsTheRelayWithoutATeamGame()
        {
            var spiel = new Game { Id = Guid.NewGuid(), IsTeamGame = false };
            foreach (var team in new[] { Rot, Blau })
            {
                spiel.GameTeams.Add(new GameTeam { Id = team, GameId = spiel.Id, Name = team.ToString() });
                spiel.PlayerXGames.Add(new PlayerXGame { PlayerId = Guid.NewGuid(), GameTeamId = team });
            }

            Assert.AreEqual(MinigameChecks.OnlyInTeamGame, MinigameChecks.ForTeams(Frage("A", "B", "C"), spiel).Single().Code);

            spiel.IsTeamGame = true;
            Assert.AreEqual(0, MinigameChecks.ForTeams(Frage("A", "B", "C"), spiel).Count(), "Zwei besetzte Teams fallen auf.");
            StringAssert.Contains(MinigameProfiles.RulesFor(Frage(), spiel), "Im Teamspiel holt das Team mit den meisten Teilen die Punkte, aufgeteilt auf alle.");
        }
    }
}
