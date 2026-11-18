using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Base.States.Minispiele;
using LocalBuzzer.Service.Hubs.Accessors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using System.Text.Json;

namespace Quizzer.LogicUnitTests.LocalBuzzer.Base
{
    /// <summary>
    /// Das Stellvertreter-Duell am Server (M37 T10) - die Missbrauchsfälle des Entwurfs: eine Wahl durch ein Nicht-Mitglied, eine
    /// fremde Wahl (Gegner, ohne Team, unbekannt, beim Start getrennt), eine zweite Wahl fürs Team, eine Wahl nach dem Schluss,
    /// ein Los mit eingereichter Zufallszahl, eine Abgabe, wer nicht gewählt ist, Team oder Spieler in der Nutzlast, die Grenze
    /// der Wahlversuche. Dazu: der Spielleiter lost offene Teams, die Disziplin hängt an der Zahl der Teams, jedes Telefon sieht
    /// die Stellvertreter, die Siegerteams erst am Ende.
    /// </summary>
    [TestClass]
    public class StellvertreterStateUnitTests
    {
        private Player anna = null!;
        private Player bert = null!;
        private Player carl = null!;
        private Player fritz = null!;
        private Player dora = null!;
        private Player emil = null!;
        private Game game = null!;
        private GameTeam rot = null!;
        private GameTeam blau = null!;
        private StellbareUhr uhr = null!;
        private TeamRoster aufstellung = TeamRoster.Empty;
        private MinigameState spiel = null!;
        private readonly List<(int Von, int Bis)> lose = new();
        private int losErgebnis;

        [TestInitialize]
        public void Aufbau()
        {
            anna = Spieler("Anna");
            bert = Spieler("Bert");
            carl = Spieler("Carl");
            fritz = Spieler("Fritz");
            dora = Spieler("Dora");
            emil = Spieler("Emil");
            uhr = new StellbareUhr();
            game = new Game { Id = Guid.NewGuid(), IsTeamGame = true };
            rot = new GameTeam { Id = Guid.NewGuid(), GameId = game.Id, Name = "Rot", ColorIndex = 5 };
            blau = new GameTeam { Id = Guid.NewGuid(), GameId = game.Id, Name = "Blau", ColorIndex = 4 };
            game.GameTeams.AddRange([rot, blau]);

            // Anna und Bert sind Rot, Carl und Fritz Blau, Dora ohne Team; Emil ist Blau, aber beim Start getrennt.
            foreach (var (p, team) in new[] { (anna, rot), (bert, rot), (carl, blau), (fritz, blau), (dora, (GameTeam?)null), (emil, blau) })
                game.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = p.Id, Player = p, GameTeamId = team?.Id });

            emil.ConnectionState = PlayerConnection.Disconnected;
            aufstellung = TeamRoster.From(game);

            // Die Wartezeit der Reaktion wuerfelt mit 2000 als Untergrenze - gelost wird ab 0.
            spiel = new MinigameState(new GameAccessor { GetGame = () => game }, () => aufstellung)
            {
                Uhr = uhr,
                Zufall = (von, bis) =>
                {
                    if (von != 0)
                        return von;

                    lose.Add((von, bis));
                    return Math.Min(losErgebnis, bis - 1);
                },
            };
        }

        private static Player Spieler(string name)
            => new() { Id = Guid.NewGuid(), Designation = name, DisplayName = name, ConnectionState = PlayerConnection.Connected };

        private void Starte(int variante = StellvertreterEvaluator.ReaktionVariant, int runden = 1)
        {
            spiel.Configure(new MinigameSetup(MinigameKind.ProxyDuel, "Stellvertreter-Duell", "Regel", runden, Variant: variante));
            spiel.Reset();
            Assert.IsTrue(spiel.StartNextRound(), "Das Stellvertreter-Duell liess sich nicht starten.");
        }

        private MinigameSubmitResult Gib(Player p, object nutzlast)
            => spiel.Submit(p, new MinigameAction
            {
                Kind = MinigameKind.ProxyDuel,
                Durchgang = spiel.Durchgang,
                Payload = JsonSerializer.SerializeToElement(nutzlast),
            });

        private MinigameSubmitResult Waehle(Player wer, Player? wen) => Gib(wer, new { chosen = wen?.Id, lot = false });

        private MinigameSubmitResult Reagiere(Player wer, int ms) => Gib(wer, new { falseStart = false, milliseconds = ms });

        private ProxyDuelRules Regeln => (ProxyDuelRules)spiel.Rules!;

        private ProxyDuelClientInfo Info(Player? p) => (ProxyDuelClientInfo)((MinigameClientInfo)spiel.InfoFor(p)).KindInfo!;

        private string? Zuschauertext(Player p) => ((MinigameClientInfo)spiel.InfoFor(p)).WatcherText;

        private Guid? VertreterVon(GameTeam team) => Regeln.Vertreter.SingleOrDefault(v => v.TeamId == team.Id)?.SpielerId;

        /// <summary>Rot schickt Bert, Blau Carl - danach spielen nur die beiden.</summary>
        private void BeideWaehlen()
        {
            Assert.IsTrue(Waehle(anna, bert).Accepted, "Rot konnte Bert nicht wählen.");
            var schluss = Waehle(fritz, carl);
            Assert.IsTrue(schluss.Accepted && schluss.Restarted, "Mit der letzten Wahl beginnt die Disziplin nicht als neue Stufe.");
        }

        // --- Wer wählt ---

        [TestMethod]
        public void OnlyMembersConnectedAtTheStartChooseForTheirTeam()
        {
            Starte();

            Assert.IsTrue(Regeln.Plays(anna.Id) && Regeln.Plays(bert.Id) && Regeln.Plays(carl.Id) && Regeln.Plays(fritz.Id));
            Assert.IsFalse(Waehle(dora, dora).Accepted, "Dora ohne Team hat gewählt.");
            Assert.IsTrue(spiel.IsLockedFor(dora), "Doras Telefon ist nicht gesperrt.");
            StringAssert.Contains(Zuschauertext(dora), "in keinem Team");
            Assert.IsFalse(Info(dora).ChoosesProxy);
            Assert.AreEqual("Die Stellvertreter spielen – du schaust zu.", ((MinigameClientInfo)spiel.InfoFor(null)).WatcherText, "Der Blick von außen ist ohne Team.");

            // Emil kommt nach dem Start wieder - er wählt nicht, er wird auch nicht gewählt.
            emil.ConnectionState = PlayerConnection.Connected;
            Assert.IsFalse(Waehle(emil, emil).Accepted, "Emil wählte, obwohl er beim Start getrennt war.");
            Assert.IsFalse(Waehle(carl, emil).Accepted, "Emil wurde gewählt, obwohl er beim Start getrennt war.");
            Assert.AreEqual(0, Regeln.Vertreter.Count);
        }

        /// <summary>Die Regel prüft selbst noch einmal - nicht nur der Rahmen über <c>Plays</c>.</summary>
        [TestMethod]
        public void TheRulesRejectANonMemberAndASecondChoiceOnTheirOwn()
        {
            Starte();

            Assert.IsNull(Regeln.Accept(dora, JsonSerializer.SerializeToElement(new { chosen = anna.Id })), "Die Regel nahm Doras Wahl an.");
            Assert.IsNotNull(Regeln.Accept(anna, JsonSerializer.SerializeToElement(new { chosen = bert.Id })));
            Assert.IsNull(Regeln.Accept(bert, JsonSerializer.SerializeToElement(new { chosen = anna.Id })), "Die Regel nahm eine zweite Wahl für Rot an.");
            Assert.AreEqual(bert.Id, VertreterVon(rot));
        }

        [TestMethod]
        public void AForeignProxyIsRejected()
        {
            Starte();

            Assert.IsFalse(Waehle(anna, carl).Accepted, "Rot hat Carl aus Blau gewählt.");
            Assert.IsFalse(Waehle(anna, dora).Accepted, "Rot hat Dora ohne Team gewählt.");
            Assert.IsFalse(Gib(anna, new { chosen = Guid.NewGuid() }).Accepted, "Rot hat eine unbekannte Kennung gewählt.");
            Assert.IsFalse(Gib(anna, new { chosen = (Guid?)null }).Accepted, "Eine leere Wahl wurde angenommen.");
            Assert.IsFalse(Gib(bert, "Bert").Accepted, "Eine kaputte Nutzlast wurde angenommen.");
            Assert.AreEqual(0, Regeln.Vertreter.Count);

            var wahl = Waehle(anna, anna);
            Assert.IsTrue(wahl.Accepted, "Anna konnte sich selbst nicht wählen.");
            Assert.AreEqual(new Stellvertreter(rot.Id, anna.Id, false), ((ProxyChosen)wahl.Value!).Vertreter);
            Assert.IsTrue(wahl.ForAll && !wahl.Restarted, "Eine Wahl, nach der noch Teams fehlen, geht nicht an alle oder startet neu.");
        }

        [TestMethod]
        public void ATeamChoosesOnlyOnce()
        {
            Starte();

            Assert.IsTrue(Waehle(anna, bert).Accepted);
            Assert.IsFalse(Waehle(bert, anna).Accepted, "Bert hat die Wahl von Rot überschrieben.");
            Assert.IsFalse(Waehle(anna, anna).Accepted, "Anna hat ein zweites Mal gewählt.");
            Assert.AreEqual(bert.Id, VertreterVon(rot));

            Assert.IsTrue(spiel.IsLockedFor(anna), "Annas Telefon wählt nach der Wahl ihres Teams weiter.");
            Assert.AreEqual("Euer Stellvertreter: Bert – die anderen Teams wählen noch.", Zuschauertext(anna));
            Assert.IsFalse(spiel.IsLockedFor(carl), "Blau darf nach der Wahl von Rot nicht mehr wählen.");
        }

        [TestMethod]
        public void AChoiceAfterTheCloseIsRejectedAndChangesNothing()
        {
            Starte();
            BeideWaehlen();

            Assert.IsFalse(Waehle(anna, anna).Accepted, "Nach dem Schluss der Wahl wurde gewählt.");
            Assert.IsFalse(Waehle(bert, anna).Accepted, "Der Stellvertreter hat nach dem Schluss gewählt.");
            Assert.IsFalse(Gib(carl, new { lot = true }).Accepted, "Nach dem Schluss wurde gelost.");
            Assert.AreEqual((bert.Id, carl.Id), (VertreterVon(rot), VertreterVon(blau)), "Eine späte Wahl hat einen Stellvertreter getauscht.");
            Assert.IsFalse(spiel.OpenNextStage(r => (r as ProxyDuelRules)?.LoseOffene() == true), "Nach dem Schluss lost der Spielleiter neu.");
        }

        /// <summary>Gelost wird mit der Zufallsquelle des Servers unter den Mitgliedern - was die Nachricht mitbringt, lenkt nichts.</summary>
        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        public void TheLotIsDrawnOnTheServerOnly(int zahl)
        {
            losErgebnis = zahl;
            Starte();

            var kandidaten = new[] { anna.Id, bert.Id }.Order().ToList();
            var anderer = kandidaten[1 - zahl];

            var ergebnis = Gib(anna, new { lot = true, chosen = anderer, seed = 1 - zahl, index = 1 - zahl, zufall = 1 - zahl });

            Assert.IsTrue(ergebnis.Accepted);
            Assert.AreEqual(new Stellvertreter(rot.Id, kandidaten[zahl], true), ((ProxyChosen)ergebnis.Value!).Vertreter,
                "Das Los folgte der Nachricht statt der Zufallsquelle des Servers.");
            Assert.AreEqual((0, 2), lose.Single(), "Gelost wurde nicht genau unter den zwei Mitgliedern von Rot.");
        }

        // --- Wer spielt ---

        [TestMethod]
        public void OnlyTheProxiesPlayTheDiscipline()
        {
            Starte();
            BeideWaehlen();

            Assert.IsFalse(Reagiere(anna, 150).Accepted, "Anna aus Rot hat für ihren Stellvertreter gespielt.");
            Assert.IsFalse(Reagiere(fritz, 150).Accepted, "Fritz aus Blau hat für seinen Stellvertreter gespielt.");
            Assert.IsFalse(Reagiere(dora, 150).Accepted, "Dora ohne Team hat gespielt.");
            Assert.IsTrue(spiel.IsLockedFor(anna) && spiel.IsLockedFor(dora), "Ein Zuschauer ist nicht gesperrt.");
            Assert.AreEqual("Bert spielt für euer Team – du schaust zu.", Zuschauertext(anna));
            StringAssert.Contains(Zuschauertext(dora), "in keinem Team");

            Assert.IsTrue(Reagiere(bert, 300).Accepted, "Bert als Stellvertreter wurde verworfen.");
            var letzte = Reagiere(carl, 250);
            Assert.IsTrue(letzte.Accepted);
            Assert.IsNotNull(letzte.Closed, "Die Runde schliesst nicht, wenn beide Stellvertreter abgegeben haben.");
            Assert.AreEqual(MinigamePhase.Finished, spiel.Phase);

            var ergebnis = Regeln.Ergebnis();
            CollectionAssert.AreEquivalent(new[] { carl.Id }, ergebnis.Sieger.ToList());
            Assert.IsTrue(Info(fritz).IsWinner, "Fritz erfährt nicht, dass sein Team gewonnen hat.");
            Assert.IsFalse(Info(anna).IsWinner);
        }

        [TestMethod]
        public void TheRulesRejectANonProxyOnTheirOwn()
        {
            Starte();
            BeideWaehlen();

            var zeit = JsonSerializer.SerializeToElement(new { falseStart = false, milliseconds = 200 });
            Assert.IsNull(Regeln.Accept(anna, zeit), "Die Regel nahm Annas Zeit an.");
            Assert.IsNotNull(Regeln.Accept(bert, zeit));
        }

        /// <summary>Team und Spieler sagt die Aufstellung des Hubs, nie die Nachricht.</summary>
        [TestMethod]
        public void ATeamOrPlayerInThePayloadIsIgnored()
        {
            Starte();

            Assert.IsTrue(Gib(anna, new { chosen = bert.Id, lot = false, teamId = blau.Id, playerId = carl.Id }).Accepted);
            Assert.AreEqual(bert.Id, VertreterVon(rot), "Die Wahl ging nicht an Rot.");
            Assert.IsNull(VertreterVon(blau), "Die Nutzlast hat für Blau gewählt.");
        }

        /// <summary>Höchstens fünf Wahlversuche je zehn Sekunden Serveruhr und Telefon - auch verworfene zählen.</summary>
        [TestMethod]
        public void AtMostFiveTriesPerWindow()
        {
            Starte();

            // Die Zahlen aus dem Entwurf (Abschnitt 4), nicht die Konstante - sonst misst die Zusicherung sie an sich selbst.
            for (var i = 0; i < 5; i++)
                Assert.IsFalse(Gib(anna, new { chosen = Guid.NewGuid() }).Accepted);

            Assert.IsFalse(Waehle(anna, bert).Accepted, "Der sechste Versuch im Fenster wurde angenommen.");

            uhr.Vor(TimeSpan.FromSeconds(9.9));
            Assert.IsFalse(Waehle(anna, bert).Accepted, "Vor dem Ende des Fensters wurde Anna wieder angenommen.");
            uhr.Vor(TimeSpan.FromSeconds(0.1));
            Assert.IsTrue(Waehle(anna, bert).Accepted, "Nach dem Fenster wird Anna weiter abgewiesen.");
        }

        // --- Der Spielleiter ---

        [TestMethod]
        public void TheGameMasterDrawsForOpenTeamsOnly()
        {
            spiel.Configure(new MinigameSetup(MinigameKind.ProxyDuel, "Stellvertreter-Duell", "Regel", 1));
            spiel.Reset();
            Assert.IsFalse(spiel.OpenNextStage(r => (r as ProxyDuelRules)?.LoseOffene() == true), "Vor dem Start wurde gelost.");

            Assert.IsTrue(spiel.StartNextRound());
            Assert.IsTrue(Waehle(anna, bert).Accepted);
            var durchgang = spiel.Durchgang;

            Assert.IsTrue(spiel.OpenNextStage(r => (r as ProxyDuelRules)?.LoseOffene() == true), "Der Spielleiter konnte nicht losen.");

            Assert.AreEqual(durchgang + 1, spiel.Durchgang, "Nach dem Losen gilt kein neuer Durchgang.");
            Assert.AreEqual(bert.Id, VertreterVon(rot), "Das Losen hat die Wahl von Rot überschrieben.");
            Assert.IsTrue(VertreterVon(blau) == carl.Id || VertreterVon(blau) == fritz.Id, "Blau bekam keinen der Seinen.");
            Assert.IsTrue(Regeln.Vertreter.Single(v => v.TeamId == blau.Id).Gelost);
            Assert.IsFalse(Regeln.WahlOffen);
            Assert.AreEqual(1, lose.Count, "Rot wurde mitgelost.");
        }

        // --- Disziplin und Anzeige ---

        [TestMethod]
        public void RockPaperScissorsWithTwoTeamsIsSecretUntilTheRoundCloses()
        {
            Starte(StellvertreterEvaluator.SchereSteinPapierVariant, runden: 3);
            BeideWaehlen();
            Assert.AreEqual(DuellEvaluator.SchereSteinPapierVariant, Regeln.Disziplin);

            Assert.IsFalse(Reagiere(bert, 200).Accepted, "Eine Zeit wurde als Zug angenommen.");
            Assert.IsTrue(Gib(bert, new { move = (int)SchereSteinPapier.Stein }).Accepted);
            Assert.IsNull(Info(carl).OtherMove, "Carl sieht Berts Zug, bevor die Runde zu ist.");
            Assert.AreEqual(SchereSteinPapier.Stein, Info(bert).OwnMove);

            Assert.IsNotNull(Gib(carl, new { move = (int)SchereSteinPapier.Schere }).Closed);
            Assert.AreEqual(SchereSteinPapier.Stein, Info(carl).OtherMove, "Nach der Runde fehlt der fremde Zug.");
            Assert.AreEqual((1, 0), (Info(bert).OwnWins, Info(bert).OtherWins));

            Assert.IsTrue(spiel.StartNextRound());
            Gib(bert, new { move = (int)SchereSteinPapier.Papier });
            Gib(carl, new { move = (int)SchereSteinPapier.Stein });
            Assert.AreEqual(MinigamePhase.Finished, spiel.Phase, "2:0 bei Best of 3 endet nicht.");
            Assert.IsTrue(Info(anna).IsWinner, "Anna erfährt nicht, dass Rot gewonnen hat.");
        }

        [TestMethod]
        public void ThreeTeamsPlayReactionEvenWhenRockPaperScissorsIsSet()
        {
            var gelb = new GameTeam { Id = Guid.NewGuid(), GameId = game.Id, Name = "Gelb", ColorIndex = 3 };
            game.GameTeams.Add(gelb);
            game.PlayerXGames.Single(x => x.PlayerId == dora.Id).GameTeamId = gelb.Id;
            aufstellung = TeamRoster.From(game);

            Starte(StellvertreterEvaluator.SchereSteinPapierVariant);

            Assert.AreEqual(DuellEvaluator.ReaktionVariant, Regeln.Disziplin, "Drei Teams spielen Schere, Stein, Papier.");
            Assert.AreEqual(3, Regeln.Teams.Count);
        }

        [TestMethod]
        public void EachPhoneSeesItsOwnTeamsOptionsAndTheProxies()
        {
            Starte();

            var vorher = Info(anna);
            Assert.IsTrue(vorher.ChoosesProxy);
            CollectionAssert.AreEquivalent(new[] { "Anna", "Bert" }, vorher.Options.Select(o => o.Name).ToList(), "Anna sieht nicht genau ihr Team zur Wahl.");
            Assert.AreEqual(2, vorher.Teams.Count);
            Assert.IsTrue(vorher.Teams.Single(t => t.Name == "Rot").IsOwn && !vorher.Teams.Single(t => t.Name == "Blau").IsOwn);
            Assert.AreEqual((5, "✚"), (vorher.Teams.Single(t => t.Name == "Rot").ColorIndex, vorher.Teams.Single(t => t.Name == "Rot").Symbol));
            Assert.AreEqual(0, Info(dora).Options.Count, "Dora ohne Team bekommt Tasten.");
            Assert.AreEqual(0, Info(null).Options.Count, "Der Blick von aussen bekommt Tasten.");

            Waehle(anna, bert);
            Assert.AreEqual("Bert", Info(carl).Teams.Single(t => t.Name == "Rot").ProxyName, "Blau sieht nicht, wen Rot schickt.");
            Assert.IsNull(Info(carl).Teams.Single(t => t.Name == "Blau").ProxyName);

            Gib(carl, new { lot = true });
            Assert.IsTrue(Info(bert).IsProxy && !Info(anna).IsProxy);
            Assert.IsNotNull(Info(bert).Reaction, "Der Stellvertreter bekommt keine Reaktion.");
            Assert.IsNull(Info(anna).Reaction, "Ein Zuschauer bekommt die Reaktion.");
            Assert.IsTrue(Info(anna).Teams.Single(t => t.Name == "Blau").ByLot);
            Assert.IsTrue(Info(anna).Teams.All(t => !t.IsWinner), "Vor dem Ende ist ein Team Sieger.");
        }

        /// <summary>Wiederholen behält die Stellvertreter, Zurücksetzen wählt neu - und hält fest, wer jetzt verbunden ist.</summary>
        [TestMethod]
        public void RepeatKeepsTheProxiesAndResetChoosesAgain()
        {
            Starte();
            BeideWaehlen();

            Assert.IsTrue(spiel.RepeatRound());
            Assert.AreEqual((bert.Id, carl.Id), (VertreterVon(rot), VertreterVon(blau)), "Wiederholen hat die Stellvertreter vergessen.");
            Assert.IsTrue(Reagiere(bert, 200).Accepted);

            emil.ConnectionState = PlayerConnection.Connected;
            spiel.Reset();
            Assert.IsTrue(spiel.StartNextRound());
            Assert.IsTrue(Regeln.WahlOffen, "Nach dem Zurücksetzen wird nicht neu gewählt.");
            Assert.IsTrue(Waehle(emil, emil).Accepted, "Emil, jetzt verbunden, kann nach dem Zurücksetzen nicht wählen.");
        }

        [TestMethod]
        public void WithoutATeamGameNobodyPlays()
        {
            aufstellung = TeamRoster.Empty;
            Starte();

            Assert.AreEqual(0, Regeln.Teams.Count);
            Assert.IsFalse(Waehle(anna, anna).Accepted, "Ohne Teamspiel wählte Anna.");
            Assert.IsFalse(Reagiere(anna, 200).Accepted, "Ohne Teamspiel spielte Anna.");
            Assert.AreEqual(0, Info(anna).Teams.Count);
        }
    }
}
