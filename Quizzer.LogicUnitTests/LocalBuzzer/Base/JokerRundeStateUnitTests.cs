using LocalBuzzer.Service.Base.States.Joker;
using LocalBuzzer.Service.Base.States.Minispiele;
using LocalBuzzer.Service.Hubs.Accessors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Quizzer.LogicUnitTests.LocalBuzzer.Base
{
    /// <summary>
    /// Die Joker-Runde am Server ohne Netz (M35 C7b): wer spielt, wann sie schliesst, und die Missbrauchsfaelle -
    /// fremder, unbekannter oder zweiter Joker, falsches Ziel, Abgabe nach dem Schliessen, Ratenbegrenzung, Spieler aus
    /// der Verbindung, und das Geheimnis bis zum Schluss (Gegenprobe zweier Abgaben, die sich nur darin unterscheiden).
    /// </summary>
    [TestClass]
    public class JokerRundeStateUnitTests
    {
        private Player anna = null!;
        private Player bert = null!;
        private Player clara = null!;
        private Game game = null!;
        private readonly JokerOffer annasDoppelt = new(Guid.NewGuid(), JokerKind.Double);
        private readonly JokerOffer annasSperre = new(Guid.NewGuid(), JokerKind.Block);
        private readonly JokerOffer bertsSchutz = new(Guid.NewGuid(), JokerKind.Shield);
        private StellbareUhr uhr = null!;
        private MinigameState zustand = null!;

        [TestInitialize]
        public void Aufbau()
        {
            anna = Spieler("Anna");
            bert = Spieler("Bert");
            clara = Spieler("Clara");
            game = new Game { Id = Guid.NewGuid() };

            foreach (var p in new[] { anna, bert, clara })
                game.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = p.Id, Player = p });

            uhr = new StellbareUhr();
            zustand = Neu();
        }

        private static Player Spieler(string name)
            => new() { Id = Guid.NewGuid(), Designation = name, DisplayName = name, ConnectionState = PlayerConnection.Connected };

        /// <summary>Anna haelt Doppelt und Sperre, Bert den Schutz, Clara nichts - die Runde laeuft.</summary>
        private MinigameState Neu()
        {
            var neu = new MinigameState(new GameAccessor { GetGame = () => game }) { Uhr = uhr };
            neu.Configure(new MinigameSetup(default, "Joker-Runde", "Wer setzt einen Joker?", 1, CountdownSeconds: 0,
                Jokers: new Dictionary<Guid, IReadOnlyList<JokerOffer>>
                {
                    [anna.Id] = [annasDoppelt, annasSperre],
                    [bert.Id] = [bertsSchutz],
                    [clara.Id] = [],
                }));
            neu.Reset();
            Assert.IsTrue(neu.StartNextRound(), "Die Joker-Runde liess sich nicht starten.");
            return neu;
        }

        private static MinigameSubmitResult Gib(MinigameState z, Player p, Guid? joker, Guid? ziel = null, object? extra = null)
            => z.Submit(p, new MinigameAction
            {
                PrePhase = MinigamePrePhase.Joker,
                Durchgang = z.Durchgang,
                Payload = JsonSerializer.SerializeToElement(extra ?? new { jokerId = joker, target = ziel }),
            });

        private MinigameSubmitResult Gib(Player p, Guid? joker, Guid? ziel = null) => Gib(zustand, p, joker, ziel);

        private static JokerClientInfo Info(MinigameState z, Player? p) => (JokerClientInfo)((MinigameClientInfo)z.InfoFor(p)).KindInfo!;

        /// <summary>Der Zustand, wie der Hub ihn schickt - ohne den Durchgang, der je Instanz zufaellig beginnt und kein Geheimnis ist.</summary>
        private static string Json(MinigameState z, Player? p)
        {
            var knoten = JsonSerializer.SerializeToNode(z.InfoFor(p), new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
            Assert.IsTrue(knoten.Remove("durchgang"), "Der Durchgang heisst anders - die Probe vergliche ihn mit.");
            return knoten.ToJsonString();
        }

        /// <summary>Was der Spielleiter nach dem Schliessen eingeloest hat (V12) - der Rest gilt als abgelehnt.</summary>
        private static bool Loese(MinigameState z, params Guid[] eingeloest) => z.Lenke(r => ((JokerRules)r!).ConfirmRedeemed(eingeloest));

        [TestMethod]
        public void OnlyHoldersDecideAndTheRoundClosesWhenAllHoldersHaveDecided()
        {
            Assert.AreEqual(MinigamePrePhase.Joker, ((MinigameClientInfo)zustand.InfoFor(null)).PrePhase, "Das Telefon erfaehrt nicht, dass es die Joker-Runde ist.");
            Assert.IsTrue(zustand.IsLockedFor(clara), "Clara ohne Joker darf in der Joker-Runde abgeben.");
            Assert.AreEqual(JokerRules.ZuschauerText, ((MinigameClientInfo)zustand.InfoFor(clara)).WatcherText);
            Assert.IsFalse(Gib(clara, null).Accepted, "Clara ohne Joker hat abgegeben.");

            var erste = Gib(anna, annasDoppelt.Id);
            Assert.IsTrue(erste.Accepted);
            Assert.IsNull(erste.Closed, "Die Runde schloss, bevor Bert entschieden hat.");

            var zweite = Gib(bert, null);
            Assert.IsTrue(zweite.Accepted, "„Keinen Joker“ wurde verworfen.");
            Assert.IsNotNull(zweite.Closed, "Die Runde schloss nicht, als alle Halter entschieden hatten.");

            var abgaben = zweite.Closed!.Submissions;
            Assert.AreEqual(new JokerChoice(anna.Id, annasDoppelt.Id, JokerKind.Double, null), abgaben[anna.Id]);
            Assert.AreEqual(new JokerChoice(bert.Id, null, null, null), abgaben[bert.Id]);
        }

        [TestMethod]
        public void AForeignOrUnknownJokerIsRejected()
        {
            Assert.IsFalse(Gib(bert, annasDoppelt.Id).Accepted, "Bert hat Annas Joker eingesetzt.");
            Assert.IsFalse(Gib(bert, Guid.NewGuid()).Accepted, "Ein unbekannter (oder schon eingeloester) Joker wurde angenommen.");
            Assert.IsFalse(zustand.Rules!.HasSubmitted(bert.Id), "Eine verworfene Abgabe zaehlt als Entscheidung.");
        }

        [TestMethod]
        public void ASecondJokerInTheSameQuestionIsRejected()
        {
            Assert.IsTrue(Gib(anna, annasDoppelt.Id).Accepted);
            Assert.IsFalse(Gib(anna, annasSperre.Id, bert.Id).Accepted, "Anna hat einen zweiten Joker eingesetzt.");
            Assert.AreEqual(JokerKind.Double, ((JokerChoice)zustand.Rules!.CurrentSubmissions[anna.Id]).Kind, "Der zweite Joker hat den ersten ersetzt.");
        }

        [TestMethod]
        public void ABlockOnSelfOnANonPlayerOrWithoutTargetIsRejected()
        {
            Assert.IsFalse(Gib(anna, annasSperre.Id, anna.Id).Accepted, "Sperre gegen sich selbst angenommen.");
            Assert.IsFalse(Gib(anna, annasSperre.Id, Guid.NewGuid()).Accepted, "Sperre gegen einen Nicht-Mitspieler angenommen.");
            Assert.IsFalse(Gib(anna, annasSperre.Id).Accepted, "Sperre ohne Ziel angenommen.");
            Assert.IsFalse(Gib(anna, annasDoppelt.Id, bert.Id).Accepted, "Doppelt mit Ziel angenommen.");
            Assert.IsFalse(Gib(anna, null, bert.Id).Accepted, "„Keinen Joker“ mit Ziel angenommen.");

            // Fuenf Fehlversuche erschoepfen die Plausibilitaetsgrenze - erst nach dem Fenster zaehlt der richtige.
            uhr.Vor(JokerRules.Fenster);
            Assert.IsTrue(Gib(anna, annasSperre.Id, clara.Id).Accepted, "Die richtige Sperre wurde verworfen.");
        }

        [TestMethod]
        public void ASubmissionAfterTheRoundClosedIsRejected()
        {
            Assert.IsNotNull(zustand.CloseRound(), "Der Spielleiter konnte die Runde nicht schliessen.");

            Assert.IsFalse(Gib(anna, annasDoppelt.Id).Accepted, "Nach dem Schliessen wurde ein Joker angenommen.");
            Assert.IsFalse(zustand.Rules!.HasSubmitted(anna.Id));
        }

        [TestMethod]
        public void ASubmissionForAnotherPrePhaseIsRejected()
        {
            var ohne = zustand.Submit(anna, new MinigameAction
            {
                Durchgang = zustand.Durchgang,
                Payload = JsonSerializer.SerializeToElement(new { jokerId = annasDoppelt.Id }),
            });

            Assert.IsFalse(ohne.Accepted, "Eine Abgabe ohne prePhase gilt in der Joker-Runde.");
        }

        [TestMethod]
        public void TheRateLimitHoldsPerPlayer()
        {
            for (var i = 0; i < JokerRules.MaxVersucheJeFenster; i++)
                Assert.IsFalse(Gib(anna, Guid.NewGuid()).Accepted);

            Assert.IsFalse(Gib(anna, annasDoppelt.Id).Accepted, "Nach zu vielen Versuchen wurde trotzdem angenommen.");
            Assert.IsTrue(Gib(bert, bertsSchutz.Id).Accepted, "Annas Versuche sperren Bert - die Grenze ist nicht je Spieler.");

            uhr.Vor(JokerRules.Fenster);
            Assert.IsTrue(Gib(anna, annasDoppelt.Id).Accepted, "Nach dem Fenster bleibt Anna gesperrt.");
        }

        [TestMethod]
        public void ThePlayerComesFromTheConnectionNeverFromTheMessage()
        {
            // Anna schickt Berts Joker und behauptet, Bert zu sein - zaehlt wird Anna, also verworfen.
            Assert.IsFalse(Gib(zustand, anna, null, extra: new { jokerId = bertsSchutz.Id, playerId = bert.Id }).Accepted);

            var eigene = Gib(zustand, anna, null, extra: new { jokerId = annasDoppelt.Id, playerId = bert.Id });

            Assert.IsTrue(eigene.Accepted);
            Assert.AreEqual(anna.Id, ((JokerChoice)eigene.Value!).PlayerId);
            Assert.IsFalse(zustand.Rules!.HasSubmitted(bert.Id), "Die Nachricht hat fuer Bert abgegeben.");
        }

        [TestMethod]
        public void EachPhoneSeesOnlyItsOwnJokersAndTheBlockerSeesTargets()
        {
            CollectionAssert.AreEquivalent(new[] { annasDoppelt.Id, annasSperre.Id }, Info(zustand, anna).Options.Select(o => o.Id).ToList());
            CollectionAssert.AreEquivalent(new[] { bertsSchutz.Id }, Info(zustand, bert).Options.Select(o => o.Id).ToList());
            Assert.AreEqual(0, Info(zustand, clara).Options.Count, "Clara sieht fremde Joker.");
            Assert.AreEqual(0, Info(zustand, null).Options.Count, "Der Beamer sieht Joker.");

            CollectionAssert.AreEquivalent(new[] { bert.Id, clara.Id }, Info(zustand, anna).Targets.Select(t => t.Id).ToList(),
                "Die Ziele der Sperre sind nicht genau die uebrigen Mitspieler.");
            Assert.AreEqual(0, Info(zustand, bert).Targets.Count, "Bert ohne Sperre bekommt Ziele.");
        }

        /// <summary>
        /// Gegenprobe (quizzer-testaufbau): zwei Runden, in denen Anna sich nur im Geheimnis unterscheidet - Art, dann Ziel.
        /// Vor dem Schliessen sehen Bert, Clara und der Beamer in beiden genau dasselbe; danach nicht mehr.
        /// </summary>
        [TestMethod]
        [DataRow("Art")]
        [DataRow("Ziel")]
        public void TwoRoundsThatDifferOnlyInAnnasChoiceLookTheSameToEveryoneElseUntilTheyClose(string unterschied)
        {
            var zweite = Neu();

            Assert.IsTrue(Gib(zustand, anna, annasSperre.Id, bert.Id).Accepted);
            Assert.IsTrue(unterschied == "Art"
                ? Gib(zweite, anna, annasDoppelt.Id).Accepted
                : Gib(zweite, anna, annasSperre.Id, clara.Id).Accepted);

            foreach (var wer in new[] { bert, clara, null })
                Assert.AreEqual(Json(zustand, wer), Json(zweite, wer), $"{wer?.Designation ?? "Der Beamer"} sieht vor dem Schliessen Annas Wahl ({unterschied}).");

            Assert.AreNotEqual(Json(zustand, anna), Json(zweite, anna), "Anna sieht ihre eigene Wahl nicht - die Probe misst sonst nichts.");

            zustand.CloseRound();
            zweite.CloseRound();

            // V12: zu, aber noch nicht eingeloest - weiter fuer alle gleich.
            foreach (var wer in new[] { bert, clara, null })
                Assert.AreEqual(Json(zustand, wer), Json(zweite, wer), $"{wer?.Designation ?? "Der Beamer"} sieht Annas Wahl vor dem Einloesen ({unterschied}).");

            Assert.IsTrue(Loese(zustand, annasSperre.Id));
            Assert.IsTrue(Loese(zweite, annasSperre.Id, annasDoppelt.Id));

            Assert.AreNotEqual(Json(zustand, null), Json(zweite, null),
                "Nach dem Einloesen unterscheiden sich die Zustaende nicht - die Gegenprobe ist blind.");
        }

        [TestMethod]
        public void AfterClosingEveryoneSeesWhoUsedWhatAndTheBlockedOneByWhom()
        {
            Gib(anna, annasSperre.Id, bert.Id);

            Assert.AreEqual(0, Info(zustand, null).Results.Count, "Der Beamer sieht Joker vor dem Schliessen.");
            Assert.IsNull(Info(zustand, bert).BlockedBy, "Bert erfaehrt die Sperre vor dem Schliessen.");

            Gib(bert, bertsSchutz.Id);
            Assert.AreEqual(MinigamePhase.Finished, zustand.Phase, "Die Runde ist nicht zu, obwohl alle Halter entschieden haben.");

            // V12: zu heisst noch nicht eingeloest - bis dahin steht nichts fest.
            Assert.IsFalse(Info(zustand, null).Confirmed);
            Assert.AreEqual(0, Info(zustand, null).Results.Count, "Der Beamer sieht Joker vor dem Einloesen.");
            Assert.IsNull(Info(zustand, bert).BlockedBy, "Bert erfaehrt die Sperre vor dem Einloesen.");

            Assert.IsTrue(Loese(zustand, annasSperre.Id, bertsSchutz.Id));

            Assert.IsTrue(Info(zustand, null).Confirmed);
            var beamer = Info(zustand, null).Results.Select(r => $"{r.Player}:{r.Joker}:{r.Target}").ToList();
            CollectionAssert.AreEqual(new[] { "Anna:Sperre:Bert", "Bert:Schutz:" }, beamer);
            Assert.AreEqual("Anna", Info(zustand, bert).BlockedBy, "Bert erfaehrt nicht, wer ihn gesperrt hat.");
            Assert.IsNull(Info(zustand, clara).BlockedBy, "Clara gilt als gesperrt.");
            Assert.IsFalse(new[] { anna, bert, clara }.Any(p => Info(zustand, p).OwnRejected), "Eine eingeloeste Wahl gilt als abgelehnt.");
        }

        /// <summary>
        /// V12: lehnt der zweite Riegel im WPF Annas Sperre ab, erscheint sie auf keinem Telefon und nicht am Beamer - Bert
        /// gilt nicht als gesperrt, und nur Anna erfaehrt, dass ihr Joker nicht zaehlt. Vor dem Schliessen geht nichts.
        /// </summary>
        [TestMethod]
        public void ARejectedChoiceReachesNoPhoneAndOnlyItsOwnerLearnsIt()
        {
            Gib(anna, annasSperre.Id, bert.Id);
            Assert.IsFalse(Loese(zustand, annasSperre.Id), "Eingeloest wurde, waehrend die Runde laeuft.");

            Gib(bert, bertsSchutz.Id);
            Assert.IsTrue(Loese(zustand, bertsSchutz.Id));

            CollectionAssert.AreEqual(new[] { "Bert:Schutz:" }, Info(zustand, null).Results.Select(r => $"{r.Player}:{r.Joker}:{r.Target}").ToList(),
                "Die abgelehnte Sperre steht in den Ergebnissen.");
            Assert.IsNull(Info(zustand, bert).BlockedBy, "Bert gilt als gesperrt, obwohl die Sperre nicht eingeloest ist.");
            Assert.IsTrue(Info(zustand, anna).OwnRejected, "Anna erfaehrt nicht, dass ihr Joker nicht zaehlt.");
            Assert.IsFalse(Info(zustand, bert).OwnRejected || Info(zustand, clara).OwnRejected || Info(zustand, null).OwnRejected,
                "Ein anderes Telefon oder der Beamer erfaehrt von Annas Ablehnung.");

            // Eine wiederholte Runde beginnt ohne Einloesung und ohne Ablehnung - auch nach ihrem Schliessen gilt die alte nicht.
            Assert.IsTrue(zustand.RepeatRound());
            Gib(anna, annasSperre.Id, bert.Id);
            Assert.IsFalse(Loese(zustand, annasSperre.Id), "In der wiederholten Runde wurde eingeloest, waehrend sie laeuft.");
            Gib(bert, bertsSchutz.Id);

            Assert.AreEqual(MinigamePhase.Finished, zustand.Phase);
            Assert.IsFalse(Info(zustand, null).Confirmed || Info(zustand, null).Results.Count > 0 || Info(zustand, bert).BlockedBy != null,
                "Die Einloesung der alten Runde gilt fuer die neue.");
            Assert.IsFalse(Info(zustand, anna).OwnRejected, "Die Ablehnung der alten Runde gilt fuer die neue.");

            // Abgebrochen gibt es nichts einzuloesen - und auch keine alte Ablehnung.
            Assert.IsNotNull(zustand.Abort());
            Assert.IsTrue(Info(zustand, null).Confirmed, "Nach dem Abbruch wartet das Telefon auf eine Einloesung.");
            Assert.IsFalse(Info(zustand, anna).OwnRejected, "Nach dem Abbruch gilt die alte Ablehnung.");
        }
    }
}
