using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using System.Text.Json;
using static Quizzer.LogicUnitTests.LocalBuzzer.Base.KartoffelPruefstand;

namespace Quizzer.LogicUnitTests.LocalBuzzer.Base
{
    /// <summary>
    /// Die Heisse Kartoffel am Server (M35 C6) ohne Netz: nur der Halter urteilt und gibt weiter, weitergeben nur nach einer
    /// richtigen Antwort und nie an sich selbst; alte und doppelte Abgaben zaehlen nicht; die Grenze gilt je Spieler; es
    /// knallt ueber die Fristwache, danach zaehlt nichts mehr. Die Geheimnisse prueft <see cref="KartoffelGeheimnisUnitTests"/>.
    /// </summary>
    [TestClass]
    public class KartoffelStateUnitTests
    {
        private KartoffelPruefstand p = null!;

        [TestInitialize]
        public void Aufbau() => p = new KartoffelPruefstand();

        private Player Anna => p.Anna;
        private Player Bert => p.Bert;
        private Player Carl => p.Carl;

        private void Starte(bool ohneAusloeser = false)
        {
            p.Starte(p.Spiel, ohneAusloeser: ohneAusloeser);
            p.Uhr.Vor(Vorlauf);
        }

        /// <summary>Ein Urteil mit der Nummer, die gerade gilt - verworfen wird es dann nur, weil der Falsche es schickt.</summary>
        private MinigameSubmitResult FremdesUrteil(Player wer, Player halter)
            => p.Gib(p.Spiel, wer, new { statementNumber = p.Art(halter).StatementNumber, answer = true });

        [TestMethod]
        public void OnlyTheHolderSeesTheStatementAndEveryoneSeesWhoHolds()
        {
            Starte();

            var anna = p.Art(Anna);
            Assert.IsTrue(anna.IsHolder);
            Assert.AreEqual(1, anna.StatementNumber);
            Assert.IsNotNull(anna.StatementText, "Die Halterin sieht keine Aussage.");

            foreach (var spieler in new[] { Bert, Carl, null })
            {
                var info = p.Art(spieler);
                Assert.AreEqual("Anna", info.HolderName, $"{spieler?.Designation ?? "Aussen"} sieht nicht, wer haelt.");
                Assert.IsFalse(info.IsHolder);
                Assert.IsNull(info.StatementText, $"{spieler?.Designation ?? "Aussen"} sieht die Aussage.");
                Assert.AreEqual(0, info.StatementNumber);
                Assert.AreEqual(0, info.Options.Count);
            }
        }

        /// <summary>Gemischt wird an der gemeinsamen Stelle (V13): mit lauter Nullwuerfen beginnt die Folge bei der zweiten Aussage.</summary>
        [TestMethod]
        public void TheStatementsComeShuffled()
        {
            Starte();

            Assert.AreEqual(KartoffelPruefstand.Aussagen[1].Text, p.Art(Anna).StatementText, "Die Aussagen kommen ungemischt.");
        }

        [TestMethod]
        public void AWrongAnswerBringsTheNextStatementAndNoPass()
        {
            Starte();
            var erste = p.Art(Anna).StatementText;

            var falsch = p.Urteile(p.Spiel, Anna, richtig: false);

            Assert.IsTrue(falsch.Accepted);
            Assert.AreEqual(new KartoffelAntwort(1, false), falsch.Value, "Der Spielleiter erfaehrt das Urteil nicht.");
            Assert.IsFalse(falsch.ForAll, "Ein Urteil geht an alle Telefone.");
            Assert.AreEqual(2, p.Art(Anna).StatementNumber);
            Assert.AreNotEqual(erste, p.Art(Anna).StatementText, "Nach dem Fehlgriff bleibt die Aussage stehen.");
            Assert.IsTrue(p.Art(Anna).LastWrong);
            Assert.IsFalse(p.GibWeiter(p.Spiel, Anna, Bert).Accepted, "Nach einer falschen Antwort darf sie weitergeben.");
        }

        [TestMethod]
        public void OnlyACorrectAnswerAllowsThePassAndOnlyToAnotherPlayer()
        {
            Starte();

            Assert.IsFalse(p.GibWeiter(p.Spiel, Anna, Bert).Accepted, "Ohne Antwort darf sie weitergeben.");
            Assert.AreEqual(new KartoffelAntwort(1, true), p.Urteile(p.Spiel, Anna, richtig: true).Value);

            var wahl = p.Art(Anna);
            Assert.IsTrue(wahl.Choosing);
            Assert.IsNull(wahl.StatementText, "Beim Weitergeben steht noch die Aussage da.");
            CollectionAssert.AreEqual(new[] { "Bert", "Carl" }, wahl.Options.Select(o => o.Name).ToArray());
            Assert.IsFalse(p.Gib(p.Spiel, Anna, new { statementNumber = 1, answer = true }).Accepted, "Beim Weitergeben zaehlt noch ein Urteil.");

            Assert.IsFalse(p.GibWeiter(p.Spiel, Anna, Anna).Accepted, "Sie gibt an sich selbst weiter.");
            Assert.IsFalse(p.Gib(p.Spiel, Anna, new { statementNumber = 1, passTo = Guid.NewGuid() }).Accepted, "Sie gibt an einen Fremden weiter.");

            var weiter = p.GibWeiter(p.Spiel, Anna, Bert);
            Assert.IsTrue(weiter.Accepted);
            Assert.AreEqual(new KartoffelWeitergabe(Anna.Id, Bert.Id), weiter.Value);
            Assert.IsTrue(weiter.ForAll, "Den neuen Halter erfahren nicht alle Telefone.");
            Assert.IsFalse(weiter.Restarted, "Ein Halterwechsel startet den Countdown am Telefon neu.");

            Assert.IsTrue(p.Art(Bert).IsHolder);
            Assert.AreEqual(2, p.Art(Bert).StatementNumber);
            Assert.AreEqual("Anna", p.Art(Carl).PasserName);
            Assert.IsFalse(p.Spiel.IsLockedFor(Carl), "Wer nicht haelt, ist gesperrt - der Rahmen startete bei jedem Wechsel neu.");
        }

        [TestMethod]
        public void NobodyButTheHolderCanAnswerOrPass()
        {
            Starte();

            Assert.IsFalse(FremdesUrteil(Bert, Anna).Accepted, "Bert urteilt fuer Anna.");
            p.Urteile(p.Spiel, Anna, richtig: true);
            Assert.IsFalse(p.Gib(p.Spiel, Bert, new { statementNumber = 1, passTo = Carl.Id }).Accepted, "Bert gibt Annas Kartoffel weiter.");
            Assert.IsTrue(p.Art(Anna).IsHolder);

            p.GibWeiter(p.Spiel, Anna, Bert);
            Assert.IsFalse(FremdesUrteil(Anna, Bert).Accepted, "Anna urteilt noch, nachdem sie sie los ist.");
            Assert.IsTrue(p.Urteile(p.Spiel, Bert, richtig: false).Accepted, "Der neue Halter kommt nicht zum Zug.");
        }

        /// <summary>Direkt zurueck nur, wenn sonst niemand verbunden ist - und nie an ein getrenntes Telefon.</summary>
        [TestMethod]
        public void DirectReturnOnlyWhenNobodyElseIsConnected()
        {
            Starte();
            p.Urteile(p.Spiel, Anna, richtig: true);
            p.GibWeiter(p.Spiel, Anna, Bert);
            p.Urteile(p.Spiel, Bert, richtig: true);

            CollectionAssert.AreEqual(new[] { "Carl" }, p.Art(Bert).Options.Select(o => o.Name).ToArray(), "Direkt zurueck steht zur Wahl.");
            Assert.IsFalse(p.GibWeiter(p.Spiel, Bert, Anna).Accepted, "Bert gibt direkt zurueck, obwohl Carl da ist.");

            Carl.ConnectionState = PlayerConnection.Disconnected;
            CollectionAssert.AreEqual(new[] { "Anna" }, p.Art(Bert).Options.Select(o => o.Name).ToArray(), "Ohne Carl steckt sie fest.");
            Assert.IsFalse(p.GibWeiter(p.Spiel, Bert, Carl).Accepted, "Bert gibt an ein getrenntes Telefon.");
            Assert.IsTrue(p.GibWeiter(p.Spiel, Bert, Anna).Accepted, "Ohne Carl geht es nicht zurueck.");
        }

        [TestMethod]
        public void AnOldADoubleOrABrokenSubmissionDoesNotCount()
        {
            Starte();
            var nutzlast = new { statementNumber = 1, answer = !Wahr(p.Art(Anna).StatementText) };

            Assert.IsTrue(p.Gib(p.Spiel, Anna, nutzlast).Accepted);
            p.Uhr.Vor(TimeSpan.FromSeconds(1));
            Assert.IsFalse(p.Gib(p.Spiel, Anna, nutzlast).Accepted, "Dieselbe Abgabe zaehlt zweimal.");
            Assert.AreEqual(2, p.Art(Anna).StatementNumber, "Die doppelte Abgabe schaltet weiter.");

            Assert.IsFalse(p.Gib(p.Spiel, Anna, new { statementNumber = 2, answer = true, passTo = Bert.Id }).Accepted, "Urteil und Ziel zugleich zaehlen.");
            Assert.IsFalse(p.Gib(p.Spiel, Anna, new { statementNumber = 2 }).Accepted, "Eine leere Abgabe zaehlt.");
            Assert.IsFalse(p.Gib(p.Spiel, Anna, new { statementNumber = 2, answer = true }, MinigameKind.LightningRound).Accepted, "Eine fremde Art zaehlt.");

            var alterDurchgang = p.Spiel.Durchgang;
            Assert.IsTrue(p.Spiel.RepeatRound());
            p.Uhr.Vor(Vorlauf);
            var veraltet = p.Spiel.Submit(Anna, new MinigameAction
            {
                Kind = MinigameKind.HotPotato,
                Durchgang = alterDurchgang,
                Payload = JsonSerializer.SerializeToElement(new { statementNumber = 1, answer = true }),
            });
            Assert.IsFalse(veraltet.Accepted, "Eine Abgabe aus dem alten Durchgang zaehlt.");
            Assert.AreEqual(1, p.Art(Anna).StatementNumber, "Die Wiederholung faengt nicht vorn an.");
        }

        [TestMethod]
        public void MoreAnswersThanTheLimitAreRejected()
        {
            Starte();

            for (var i = 0; i < KartoffelEvaluator.MaxAntwortenJeFenster; i++)
            {
                p.Uhr.Vor(TimeSpan.FromMilliseconds(10));
                Assert.IsTrue(p.Urteile(p.Spiel, Anna, richtig: false, warte: false).Accepted, $"Urteil {i + 1} wurde verworfen.");
            }

            var nummer = p.Art(Anna).StatementNumber;
            Assert.IsFalse(p.Urteile(p.Spiel, Anna, richtig: false, warte: false).Accepted, "Die Grenze greift nicht.");
            Assert.AreEqual(nummer, p.Art(Anna).StatementNumber, "Ein verworfenes Urteil hat weitergeschaltet.");

            p.Uhr.Vor(TimeSpan.FromSeconds(KartoffelEvaluator.FensterSekunden));
            Assert.IsTrue(p.Urteile(p.Spiel, Anna, richtig: false, warte: false).Accepted, "Nach dem Fenster geht es nicht weiter.");
        }

        /// <summary>Die Grenze gilt je Spieler: wer schnell urteilt und weitergibt, bremst den Naechsten nicht aus.</summary>
        [TestMethod]
        public void TheLimitCountsPerPlayer()
        {
            Starte();

            for (var i = 0; i < KartoffelEvaluator.MaxAntwortenJeFenster; i++)
            {
                p.Uhr.Vor(TimeSpan.FromMilliseconds(10));
                var letzte = i == KartoffelEvaluator.MaxAntwortenJeFenster - 1;
                Assert.IsTrue(p.Urteile(p.Spiel, Anna, richtig: letzte, warte: false).Accepted, $"Annas Urteil {i + 1} wurde verworfen.");
            }

            Assert.IsTrue(p.GibWeiter(p.Spiel, Anna, Bert).Accepted);

            for (var i = 0; i < KartoffelEvaluator.MaxAntwortenJeFenster; i++)
            {
                p.Uhr.Vor(TimeSpan.FromMilliseconds(10));
                Assert.IsTrue(p.Urteile(p.Spiel, Bert, richtig: false, warte: false).Accepted,
                    $"Berts Urteil {i + 1} wurde verworfen - die Grenze zaehlt alle zusammen.");
            }
        }

        [TestMethod]
        public void DuringTheCountdownNothingIsAccepted()
        {
            p.Starte(p.Spiel);
            p.Uhr.Vor(Vorlauf - TimeSpan.FromMilliseconds(1));

            Assert.IsFalse(p.Urteile(p.Spiel, Anna, richtig: true, warte: false).Accepted, "Im Countdown zaehlt ein Urteil.");
            p.Uhr.Vor(TimeSpan.FromMilliseconds(1));
            Assert.IsTrue(p.Urteile(p.Spiel, Anna, richtig: true, warte: false).Accepted, "Nach dem Countdown zaehlt kein Urteil.");
        }

        /// <summary>Der Knall kommt ueber die Fristwache; ab dem Zuender zaehlt nichts mehr, auch bevor sie schliesst.</summary>
        [TestMethod]
        public void TheBangComesFromTheDeadlineAndAfterItNothingCounts()
        {
            Starte();
            p.Urteile(p.Spiel, Anna, richtig: true);
            p.GibWeiter(p.Spiel, Anna, Bert);

            p.Bis(Vorlauf + Zuender - TimeSpan.FromMilliseconds(1));
            Assert.IsNull(p.Spiel.CloseIfDue(), "Es knallt vor dem Zuender.");
            Assert.IsTrue(p.Urteile(p.Spiel, Bert, richtig: false, warte: false).Accepted, "Kurz vor dem Knall zaehlt kein Urteil mehr.");

            p.Uhr.Vor(TimeSpan.FromMilliseconds(1));
            Assert.IsFalse(p.Urteile(p.Spiel, Bert, richtig: true, warte: false).Accepted, "Ab dem Zuender zaehlt noch ein Urteil.");

            var zu = p.Spiel.CloseIfDue();
            Assert.IsNotNull(zu, "Die Fristwache laesst es nicht knallen.");
            Assert.AreEqual(new KartoffelKnall(Bert.Id, Anna.Id), zu.Submissions[Bert.Id]);
            Assert.AreEqual(MinigamePhase.Finished, p.Spiel.Phase);
            Assert.IsFalse(p.Gib(p.Spiel, Bert, new { statementNumber = 3, answer = true }).Accepted, "Nach dem Knall zaehlt ein Urteil.");

            var auswertung = p.Spiel.Lenke(r => (r as HotPotatoRules)?.Auswertung);
            Assert.IsNotNull(auswertung);
            Assert.AreEqual(new KartoffelKnall(Bert.Id, Anna.Id), auswertung.Knall);
            Assert.AreEqual(41, auswertung.ZuenderSekunden);
            Assert.AreEqual(true, auswertung.Verlauf[0].Richtig);
            Assert.AreEqual(Bert.Id, auswertung.Verlauf[0].WeiterAn);
            Assert.IsTrue(p.Art(null).Exploded);
            Assert.AreEqual("Bert", p.Art(null).HolderName);
        }

        /// <summary>Schliesst der Spielleiter von Hand, knallt nichts - und der Zuender bleibt geheim.</summary>
        [TestMethod]
        public void ClosingByHandIsNoBang()
        {
            Starte();

            var zu = p.Spiel.CloseRound();

            Assert.AreEqual(0, zu!.Submissions.Count);
            Assert.IsNull(p.Spiel.Lenke(r => (r as HotPotatoRules)?.Auswertung)!.Knall);
            Assert.IsFalse(p.Art(null).Exploded);
            Assert.IsNull(p.Art(null).FuseSeconds, "Ohne Knall steht der Zuender da.");
        }

        /// <summary>Der Zuender liegt zwischen einem Drittel und der ganzen eingestellten Zeit - an beiden Enden gemessen.</summary>
        [TestMethod]
        [DataRow(false, 20_000)]
        [DataRow(true, 60_000)]
        public void TheFuseLiesBetweenAThirdAndTheWholeTime(bool hoechster, int millis)
        {
            var spiel = p.Neu((a, b) => b > 1000 && hoechster ? b - 1 : a);
            p.Starte(spiel);
            var regeln = (IMinigameDeadlineRules)spiel.Rules!;

            p.Uhr.Vor(Vorlauf + TimeSpan.FromMilliseconds(millis - 1));
            Assert.IsFalse(regeln.IsDue, "Es knallt zu frueh.");
            p.Uhr.Vor(TimeSpan.FromMilliseconds(1));
            Assert.IsTrue(regeln.IsDue, "Es knallt zu spaet.");
        }

        /// <summary>Ohne Ausloeser legt der Spielleiter fest, wer zuerst haelt (E1); mitten in der Runde legt er sie woanders hin.</summary>
        [TestMethod]
        public void WithoutATriggerTheMasterPicksAndCanMoveItMidRound()
        {
            Starte(ohneAusloeser: true);
            var regeln = (HotPotatoRules)p.Spiel.Rules!;

            Assert.IsNull(regeln.ChooserId);
            Assert.IsNull(p.Art(Anna).StatementText, "Ohne Halter steht eine Aussage da.");
            Assert.IsFalse(p.Gib(p.Spiel, Anna, new { statementNumber = 1, answer = true }).Accepted, "Ohne Halter wird geurteilt.");
            Assert.IsFalse(p.Spiel.SetPair(Anna.Id, Bert.Id), "Die Kartoffel nimmt ein Gegenueber an.");
            Assert.IsFalse(p.Spiel.SetPair(Guid.NewGuid(), null), "Ein Fremder haelt sie.");

            Assert.IsTrue(p.Spiel.SetPair(Carl.Id, null));
            Assert.AreEqual(Carl.Id, regeln.ChooserId);
            Assert.IsTrue(p.Art(Carl).IsHolder);
            p.Urteile(p.Spiel, Carl, richtig: true);
            p.GibWeiter(p.Spiel, Carl, Anna);

            // Der Spielleiter legt sie zu Bert: zugeschoben hat sie dann niemand.
            Assert.IsTrue(p.Spiel.SetPair(Bert.Id, null));
            Assert.IsTrue(p.Art(Bert).IsHolder);
            Assert.IsNull(p.Art(Bert).PasserName);
            Assert.IsFalse(FremdesUrteil(Anna, Bert).Accepted, "Anna urteilt noch.");

            p.Bis(Vorlauf + Zuender);
            Assert.AreEqual(new KartoffelKnall(Bert.Id, null), p.Spiel.CloseIfDue()!.Submissions[Bert.Id]);
        }
    }
}
