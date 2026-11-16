using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using static Quizzer.LogicUnitTests.LocalBuzzer.Base.KartoffelPruefstand;

namespace Quizzer.LogicUnitTests.LocalBuzzer.Base
{
    /// <summary>
    /// Die Geheimnisse der Heissen Kartoffel (M35 C6): der Zuender und die Wahrheit der Aussagen stehen in keinem Zustand -
    /// nicht beim Halter, nicht bei den anderen, nicht von aussen; der Zuender auch nicht als Restzeit. Je zweimal geprueft:
    /// mit dem entschluesselten Inhalt und mit der Gegenprobe zweier Laeufe, die sich nur im Geheimnis unterscheiden. Beide
    /// Gegenproben zeigen am Ende, dass sie einen Unterschied sehen koennten - sonst waeren sie blind (Lehre aus C5b).
    /// </summary>
    [TestClass]
    public class KartoffelGeheimnisUnitTests
    {
        private KartoffelPruefstand p = null!;

        [TestInitialize]
        public void Aufbau() => p = new KartoffelPruefstand();

        private Player?[] Alle => [p.Anna, p.Bert, p.Carl, null];

        /// <summary>
        /// Keine Zahl im Zustand ist der Zuender oder die Restzeit - in Millisekunden oder Sekunden, gerundet oder nicht -, und
        /// kein Text nennt sie. Gezaehlt wird nur ab 20: darunter liegen Art, Phase und Nummern, die nichts verraten.
        /// </summary>
        [TestMethod]
        public void TheFuseIsInNoStateNeitherAsTimeNorAsRest()
        {
            p.Starte(p.Spiel);
            Pruefe("im Countdown");
            p.Uhr.Vor(Vorlauf);
            Pruefe("zum Start");
            p.Urteile(p.Spiel, p.Anna, richtig: false);
            Pruefe("nach einem Fehlgriff");
            p.Urteile(p.Spiel, p.Anna, richtig: true);
            Pruefe("bei der Wahl");
            p.GibWeiter(p.Spiel, p.Anna, p.Bert);
            Pruefe("nach dem Weitergeben");
            p.Bis(Vorlauf + Zuender - TimeSpan.FromMilliseconds(25_500));
            Pruefe("spaet in der Runde");

            // Nach dem Knall steht er da - sonst misst die Suche nichts.
            p.Bis(Vorlauf + Zuender);
            Assert.IsNotNull(p.Spiel.CloseIfDue());
            Assert.IsTrue(Verdacht(Zustandsinhalt.Von(p.Spiel.InfoFor(null)), Zuender).Any(), "Nach dem Knall fehlt der Zuender - die Suche misst nichts.");

            void Pruefe(string wann)
            {
                foreach (var spieler in Alle)
                {
                    var treffer = Verdacht(Zustandsinhalt.Von(p.Spiel.InfoFor(spieler)), p.Rest).ToList();
                    Assert.AreEqual(0, treffer.Count,
                        $"{wann}: im Zustand von {spieler?.Designation ?? "aussen"} stehen Zuender oder Restzeit: {string.Join(", ", treffer)}");
                }
            }
        }

        /// <summary>Was im Zustand nach Zuender oder Restzeit aussieht - als Zahl oder in einem Text.</summary>
        private static IEnumerable<string> Verdacht(Zustandsinhalt inhalt, TimeSpan rest)
        {
            var werte = new[] { Zuender, Vorlauf + Zuender, rest }
                .SelectMany(z => new[] { z.TotalMilliseconds, z.TotalSeconds, Math.Floor(z.TotalSeconds), Math.Ceiling(z.TotalSeconds), Math.Round(z.TotalSeconds) })
                .Where(w => w >= 20)
                .Distinct()
                .ToList();

            foreach (var zahl in inhalt.Zahlen.Where(z => werte.Any(w => Math.Abs(w - z) < 0.0005)))
                yield return zahl.ToString();

            var ganze = werte.Where(w => w == Math.Floor(w)).Select(w => ((long)w).ToString()).ToList();
            foreach (var text in inhalt.Texte.Where(t => !Guid.TryParse(t, out _) && ganze.Any(t.Contains)))
                yield return text;
        }

        /// <summary>
        /// Keine Aussage ausser der des Halters steht in irgendeinem Zustand, von aussen gar keine, und keine Aufloesung, solange
        /// die Runde laeuft. Nach dem Knall steht sie bei allen - sonst misst die Suche nichts.
        /// </summary>
        [TestMethod]
        public void NoForeignStatementAndNoResolutionIsInAnyStateWhileTheRoundRuns()
        {
            p.Starte(p.Spiel);
            p.Uhr.Vor(Vorlauf);
            var annasErste = p.Art(p.Anna).StatementText!;
            p.Urteile(p.Spiel, p.Anna, richtig: false);
            p.Urteile(p.Spiel, p.Anna, richtig: true);
            p.GibWeiter(p.Spiel, p.Anna, p.Bert);

            var berts = p.Art(p.Bert).StatementText;
            Assert.IsNotNull(berts);
            Assert.IsTrue(Zustandsinhalt.Von(p.Spiel.InfoFor(p.Bert)).EnthaeltText(berts), "Berts Aussage steht nicht in seinem Zustand - die Probe misst nichts.");

            foreach (var spieler in Alle)
            {
                var inhalt = Zustandsinhalt.Von(p.Spiel.InfoFor(spieler));
                var eigene = spieler == p.Bert ? berts : null;

                foreach (var text in Aussagen.Select(a => a.Text).Where(t => t != eigene))
                    Assert.IsFalse(inhalt.EnthaeltText(text), $"Im Zustand von {spieler?.Designation ?? "aussen"} steht eine andere Aussage: {text}");

                Assert.AreEqual(0, p.Art(spieler).Resolution.Count, $"Waehrend der Runde steht bei {spieler?.Designation ?? "aussen"} die Aufloesung.");
            }

            p.Bis(Vorlauf + Zuender);
            Assert.IsNotNull(p.Spiel.CloseIfDue());
            Assert.IsTrue(Zustandsinhalt.Von(p.Spiel.InfoFor(null)).EnthaeltText(annasErste), "Nach dem Knall fehlt die Aufloesung - die Suche misst nichts.");
        }

        /// <summary>
        /// Die Gegenprobe zum Zuender: zwei Laeufe, die sich nur in ihm unterscheiden (41,3 s und 51,3 s), geben jedem Telefon und
        /// dem Blick von aussen bis kurz vor dem ersten Knall genau denselben Zustand. Nach beiden Knallen unterscheiden sie sich.
        /// </summary>
        [TestMethod]
        public void TwoRunsThatDifferOnlyInTheFuseLookTheSameUntilTheBang()
        {
            var frueh = p.Neu();
            var spaet = p.Neu(wurf: Wurf + 10_000);

            p.Starte(frueh);
            p.Starte(spaet);
            Gleich("im Countdown");
            p.Uhr.Vor(Vorlauf);
            Gleich("zum Start");

            Beide((z, nachher) => p.Urteile(z, p.Anna, richtig: false, warte: nachher), "nach Annas Fehlgriff");
            Beide((z, nachher) => p.Urteile(z, p.Anna, richtig: true, warte: nachher), "bei Annas Wahl");
            Beide((z, _) => p.GibWeiter(z, p.Anna, p.Bert), "nach dem Weitergeben");
            Beide((z, nachher) => p.Urteile(z, p.Bert, richtig: false, warte: nachher), "nach Berts Fehlgriff");

            p.Bis(Vorlauf + Zuender - TimeSpan.FromMilliseconds(1));
            Assert.IsNull(frueh.CloseIfDue());
            Assert.IsNull(spaet.CloseIfDue());
            Gleich("kurz vor dem ersten Knall");

            p.Bis(Vorlauf + Zuender + TimeSpan.FromSeconds(10));
            Assert.IsNotNull(frueh.CloseIfDue(), "Der fruehe Zuender knallt nicht.");
            Assert.IsNotNull(spaet.CloseIfDue(), "Der spaete Zuender knallt nicht.");
            Assert.AreNotEqual(Json(frueh, null), Json(spaet, null), "Nach dem Knall unterscheiden sich die Zustaende nicht - die Probe misst nichts.");

            void Beide(Func<MinigameState, bool, MinigameSubmitResult> schritt, string wann)
            {
                var a = schritt(frueh, true);
                var b = schritt(spaet, false);
                Assert.IsTrue(a.Accepted && b.Accepted, $"{wann}: nicht angenommen.");
                Assert.AreEqual(a.Value, b.Value, $"{wann}: an den Spielleiter geht etwas, das den Zuender traegt.");
                Gleich(wann);
            }

            void Gleich(string wann)
            {
                foreach (var spieler in Alle)
                    Assert.AreEqual(Json(frueh, spieler), Json(spaet, spieler), $"{wann}: der Zustand von {spieler?.Designation ?? "aussen"} verraet den Zuender.");
            }
        }

        /// <summary>
        /// Die Gegenprobe zur Wahrheit: zwei Inhalte, die sich nur im Haken unterscheiden (vier wahr, zwei falsch - umgedreht).
        /// Die Halter antworten in beiden so, dass richtig und falsch gleich ausgehen - denn ob eine Antwort stimmte, zeigt das
        /// Spiel selbst (weitergeben oder naechste Aussage). Dann muss jeder Zustand gleich sein; nach dem Knall unterscheiden
        /// sie sich in der Aufloesung.
        /// </summary>
        [TestMethod]
        public void TwoContentsThatDifferOnlyInTheTruthLookTheSameWhileTheRoundRuns()
        {
            var umgedreht = Aussagen.Select(a => a with { Wahr = !a.Wahr }).ToList();
            var zweite = p.Neu();

            p.Starte(p.Spiel);
            p.Starte(zweite, umgedreht);
            Gleich("im Countdown");
            p.Uhr.Vor(Vorlauf);
            Gleich("zum Start");

            Urteil(p.Anna, false);
            Urteil(p.Anna, true);
            Weiter(p.Anna, p.Bert);
            Urteil(p.Bert, false);
            Urteil(p.Bert, false);
            Urteil(p.Bert, true);
            Weiter(p.Bert, p.Carl);
            Urteil(p.Carl, false);
            Urteil(p.Carl, false);

            p.Bis(Vorlauf + Zuender);
            Assert.IsNotNull(p.Spiel.CloseIfDue());
            Assert.IsNotNull(zweite.CloseIfDue());
            Assert.AreNotEqual(Json(p.Spiel, null), Json(zweite, null), "Nach dem Knall unterscheiden sich die Zustaende nicht - die Probe misst nichts.");

            void Urteil(Player wer, bool richtig)
            {
                p.Uhr.Vor(TimeSpan.FromMilliseconds(800));
                Vergleiche(p.Urteile(p.Spiel, wer, richtig, warte: false), p.Urteile(zweite, wer, richtig, warte: false, inhalt: umgedreht),
                    $"nach dem Urteil von {wer.Designation}");
            }

            void Weiter(Player von, Player an)
                => Vergleiche(p.GibWeiter(p.Spiel, von, an), p.GibWeiter(zweite, von, an), $"nach der Weitergabe an {an.Designation}");

            void Vergleiche(MinigameSubmitResult a, MinigameSubmitResult b, string wann)
            {
                Assert.IsTrue(a.Accepted && b.Accepted, $"{wann}: nicht angenommen.");
                Assert.AreEqual(a.Value, b.Value, $"{wann}: an den Spielleiter geht etwas anderes.");
                Gleich(wann);
            }

            void Gleich(string wann)
            {
                foreach (var spieler in Alle)
                    Assert.AreEqual(Json(p.Spiel, spieler), Json(zweite, spieler), $"{wann}: der Zustand von {spieler?.Designation ?? "aussen"} verraet die Wahrheit.");
            }
        }
    }
}
