using LocalBuzzer.Service.Base;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Base;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// <b>Welche Verbindung eines Spielers gilt.</b>
    /// <para>
    /// <b>Der Fall, für den es dieses Register gibt:</b> ein Telefon schläft ein, wacht auf und
    /// verbindet neu. Dann hat derselbe Spieler kurz <i>zwei</i> Verbindungen - und wenn die
    /// alte sich verabschiedet, darf sie ihn nicht als getrennt melden. Sonst steht ein Spieler
    /// als „nicht verbunden" im Buzzer-Fenster, während sein Telefon einsatzbereit vor ihm
    /// liegt.
    /// </para>
    /// <para>
    /// <b>Bis zum 2026-09-09 hatte die Klasse keine einzige Zusicherung</b> - gemessen über
    /// beide Testprojekte: kein Treffer auf ihren Namen.
    /// </para>
    /// </summary>
    [TestClass]
    public class VerbindungsregisterUnitTests
    {
        private static Player Spieler(string name)
            => new() { Id = Guid.NewGuid(), Designation = name };

        /// <summary>Der Normalfall: eintragen, wiederfinden.</summary>
        [TestMethod]
        public void ARegisteredConnectionFindsItsPlayer()
        {
            var register = new PlayerConnectionRegistry();
            var anna = Spieler("Anna");

            Assert.IsNull(register.Register("verbindung-1", anna),
                "Bei der ersten Verbindung gibt es keine vorherige.");

            Assert.IsTrue(register.TryGetPlayer("verbindung-1", out var gefunden));
            Assert.AreSame(anna, gefunden);
        }

        /// <summary>Eine unbekannte Verbindung gehört niemandem.</summary>
        [TestMethod]
        public void AnUnknownConnectionHasNoPlayer()
        {
            var register = new PlayerConnectionRegistry();

            Assert.IsFalse(register.TryGetPlayer("gibt-es-nicht", out var gefunden));
            Assert.IsNull(gefunden);
        }

        /// <summary>
        /// <b>Der Letzte gewinnt</b>, und die vorherige Kennung kommt zurück - der Aufrufer
        /// braucht sie, um die alte Verbindung zu schließen.
        /// </summary>
        [TestMethod]
        public void TheNewestConnectionWins()
        {
            var register = new PlayerConnectionRegistry();
            var anna = Spieler("Anna");

            register.Register("alt", anna);

            Assert.AreEqual("alt", register.Register("neu", anna),
                "Die vorherige Verbindung wird nicht zurueckgemeldet - dann bleibt sie offen.");

            Assert.IsTrue(register.TryGetPlayer("neu", out _),
                "Die neue Verbindung gehoert niemandem.");

            Assert.IsFalse(register.TryGetPlayer("alt", out _),
                "Die alte Verbindung gilt weiterhin - dann buzzert ein Telefon doppelt.");
        }

        /// <summary>
        /// <b>Die teuerste Zusicherung dieser Klasse.</b> Verabschiedet sich die <i>abgelöste</i>
        /// Verbindung, darf der Spieler nicht als getrennt gelten - sein neues Telefon hängt ja
        /// dran.
        /// </summary>
        [TestMethod]
        public void AStaleDisconnectDoesNotDropTheLivePlayer()
        {
            var register = new PlayerConnectionRegistry();
            var anna = Spieler("Anna");

            register.Register("alt", anna);
            register.Register("neu", anna);

            Assert.IsFalse(register.Unregister("alt", out var abgemeldet),
                "Die abgeloeste Verbindung meldet den Spieler ab - er stuende als getrennt da, "
                + "obwohl sein Telefon verbunden ist.");

            Assert.IsNull(abgemeldet);

            Assert.IsTrue(register.TryGetPlayer("neu", out _),
                "Die lebende Verbindung ist mit verschwunden.");
        }

        /// <summary>Die aktuelle Verbindung zu lösen meldet den Spieler dagegen sehr wohl ab.</summary>
        [TestMethod]
        public void TheCurrentConnectionDoesDropThePlayer()
        {
            var register = new PlayerConnectionRegistry();
            var anna = Spieler("Anna");

            register.Register("verbindung-1", anna);

            Assert.IsTrue(register.Unregister("verbindung-1", out var abgemeldet));
            Assert.AreSame(anna, abgemeldet);

            Assert.IsFalse(register.TryGetPlayer("verbindung-1", out _));
        }

        /// <summary>
        /// Dieselbe Verbindung erneut einzutragen ist kein Wechsel - sonst schlösse der Aufrufer
        /// die Verbindung, die er gerade angemeldet hat.
        /// </summary>
        [TestMethod]
        public void RegisteringTheSameConnectionAgainChangesNothing()
        {
            var register = new PlayerConnectionRegistry();
            var anna = Spieler("Anna");

            register.Register("verbindung-1", anna);

            Assert.IsNull(register.Register("verbindung-1", anna),
                "Die eigene Verbindung wird als abzuloesende gemeldet - der Aufrufer schliesst "
                + "damit genau die, die er eben angemeldet hat.");

            Assert.IsTrue(register.TryGetPlayer("verbindung-1", out _));
        }

        /// <summary>
        /// Zwei Spieler stehen sich nicht im Weg - der Wechsel des einen lässt den anderen in
        /// Ruhe.
        /// </summary>
        [TestMethod]
        public void PlayersDoNotDisturbEachOther()
        {
            var register = new PlayerConnectionRegistry();
            var anna = Spieler("Anna");
            var bert = Spieler("Bert");

            register.Register("anna-1", anna);
            register.Register("bert-1", bert);
            register.Register("anna-2", anna);

            Assert.IsTrue(register.TryGetPlayer("bert-1", out var immerNoch),
                "Berts Verbindung ist beim Wechsel von Anna verschwunden.");

            Assert.AreSame(bert, immerNoch);
        }

        /// <summary>
        /// Ein Spieler, der geht und wiederkommt, bekommt eine saubere neue Zuordnung - und die
        /// alte Kennung wird dabei nicht faelschlich als abzuloesende gemeldet.
        /// </summary>
        [TestMethod]
        public void ComingBackAfterALeaveStartsFresh()
        {
            var register = new PlayerConnectionRegistry();
            var anna = Spieler("Anna");

            register.Register("alt", anna);
            register.Unregister("alt", out _);

            Assert.IsNull(register.Register("neu", anna),
                "Nach dem sauberen Abmelden gibt es keine vorherige Verbindung mehr - "
                + "gemeldet wird trotzdem eine, und der Aufrufer schliesst ein totes Kabel.");

            Assert.IsTrue(register.TryGetPlayer("neu", out _));
        }
    }
}
