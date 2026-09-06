using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;

namespace Quizzer.LogicUnitTests.DataModels
{
    /// <summary>
    /// Die Anmeldung und was daran hängt: welche Fragen dem angemeldeten Spielleiter zur
    /// Verfügung stehen.
    /// <para>
    /// <b>Gemeldet 2026-09-06:</b> „der moderator beim login gewählt werden also gleich zu
    /// beginn ... dem entsprechend soll nur jeder seine angelegten fragen zur verfügung haben ...
    /// damit der moderator wechseln kann".
    /// </para>
    /// <para>
    /// Der heikle Teil ist der Bestand: alle Fragen von vor der Anmeldung haben keinen Besitzer.
    /// Gehörten sie niemandem, wären sie für alle verschwunden - deshalb gehören sie allen.
    /// </para>
    /// </summary>
    [TestClass]
    public class SessionUnitTests
    {
        private Player anna = null!;
        private Player bert = null!;

        [TestInitialize]
        public void SetUp()
        {
            Session.SignOut();

            anna = new Player { Id = Guid.NewGuid(), Designation = "Anna", IsModerator = true };
            bert = new Player { Id = Guid.NewGuid(), Designation = "Bert", IsModerator = true };
        }

        [TestCleanup]
        public void TearDown() => Session.SignOut();

        private static QuestionBase Frage(Guid? besitzer)
            => new DefaultQuestion
            {
                Id = Guid.NewGuid(),
                Designation = besitzer == null ? "Gemeinsam" : "Eigen",
                OwnerPlayerId = besitzer,
            };

        /// <summary>Ohne Kennwort genügt die Auswahl.</summary>
        [TestMethod]
        public void WithoutAPasswordTheSelectionIsEnough()
        {
            Assert.IsFalse(Session.RequiresPassword(anna));
            Assert.IsTrue(Session.SignIn(anna));
            Assert.AreEqual(anna.Id, Session.CurrentModeratorId);
        }

        /// <summary>
        /// Mit Kennwort zählt es auch. Die Gegenrichtung im selben Test: das richtige lässt
        /// herein, das falsche nicht - sonst wäre nicht zu unterscheiden, ob überhaupt geprüft
        /// wird.
        /// </summary>
        [TestMethod]
        public void WithAPasswordOnlyTheRightOneGetsIn()
        {
            anna.PasswordHash = Session.HashPassword("geheim");

            Assert.IsTrue(Session.RequiresPassword(anna));

            Assert.IsFalse(Session.SignIn(anna, "falsch"), "Ein falsches Kennwort kam durch.");
            Assert.IsFalse(Session.IsSignedIn, "Trotz falschem Kennwort angemeldet.");

            Assert.IsFalse(Session.SignIn(anna, string.Empty), "Ein leeres Kennwort kam durch.");

            Assert.IsTrue(Session.SignIn(anna, "geheim"), "Das richtige Kennwort kam nicht durch.");
            Assert.AreEqual(anna.Id, Session.CurrentModeratorId);
        }

        /// <summary>
        /// Zweimal dasselbe Kennwort ergibt zwei verschiedene Ableitungen - sonst wäre am
        /// Datenbankinhalt ablesbar, wer dasselbe Kennwort benutzt.
        /// </summary>
        [TestMethod]
        public void TheSamePasswordYieldsDifferentHashes()
        {
            var a = Session.HashPassword("geheim");
            var b = Session.HashPassword("geheim");

            Assert.AreNotEqual(a, b, "Zwei Ableitungen desselben Kennworts sind gleich - es fehlt das Salz.");

            anna.PasswordHash = a;
            bert.PasswordHash = b;

            Assert.IsTrue(Session.VerifyPassword(anna, "geheim"));
            Assert.IsTrue(Session.VerifyPassword(bert, "geheim"));
        }

        /// <summary>Ein leeres Kennwort ergibt keine Ableitung, also kein Kennwort.</summary>
        [TestMethod]
        public void AnEmptyPasswordMeansNoPassword()
        {
            Assert.AreEqual(string.Empty, Session.HashPassword(string.Empty));
            Assert.AreEqual(string.Empty, Session.HashPassword(null));
        }

        /// <summary>Der Kern: eigene Fragen und die ohne Besitzer, nicht die fremden.</summary>
        [TestMethod]
        public void ASignedInModeratorSeesTheirOwnAndTheSharedOnes()
        {
            Session.SignIn(anna);

            var eigene = Frage(anna.Id);
            var fremde = Frage(bert.Id);
            var gemeinsam = Frage(null);

            Assert.IsTrue(QuestionOwnership.IsVisible(eigene), "Die eigene Frage fehlt.");
            Assert.IsTrue(QuestionOwnership.IsVisible(gemeinsam),
                "Der gemeinsame Bestand fehlt - alle Fragen von vor der Anmeldung waeren weg.");

            Assert.IsFalse(QuestionOwnership.IsVisible(fremde),
                "Die Frage eines anderen Spielleiters ist sichtbar.");

            var sichtbar = QuestionOwnership.VisibleTo([eigene, fremde, gemeinsam]);

            Assert.AreEqual(2, sichtbar.Count);
            CollectionAssert.DoesNotContain(sichtbar, fremde);
        }

        /// <summary>
        /// Der Wechsel wirkt: nach der Anmeldung als Bert sieht er seine, nicht Annas. Ohne
        /// diese Probe wäre die vorige auch dann grün, wenn der Filter auf den Ersten einrastet.
        /// </summary>
        [TestMethod]
        public void SwitchingTheModeratorSwitchesTheQuestions()
        {
            var annasFrage = Frage(anna.Id);
            var bertsFrage = Frage(bert.Id);

            Session.SignIn(anna);

            Assert.IsTrue(QuestionOwnership.IsVisible(annasFrage));
            Assert.IsFalse(QuestionOwnership.IsVisible(bertsFrage));

            Session.SignIn(bert);

            Assert.IsFalse(QuestionOwnership.IsVisible(annasFrage),
                "Nach dem Wechsel sind noch die Fragen des Vorgaengers zu sehen.");

            Assert.IsTrue(QuestionOwnership.IsVisible(bertsFrage),
                "Nach dem Wechsel fehlen die eigenen Fragen.");
        }

        /// <summary>
        /// Ohne Anmeldung gilt kein Filter. Das ist der Zustand direkt nach dem Einspielen -
        /// eine leere Fragenliste wäre dort eine Sperre, aus der man nicht herauskäme.
        /// </summary>
        [TestMethod]
        public void WithoutASignInNothingIsFilteredAway()
        {
            Assert.IsFalse(Session.IsSignedIn);

            Assert.IsTrue(QuestionOwnership.IsVisible(Frage(anna.Id)));
            Assert.IsTrue(QuestionOwnership.IsVisible(Frage(bert.Id)));
            Assert.IsTrue(QuestionOwnership.IsVisible(Frage(null)));
        }

        /// <summary>
        /// Eine neue Frage bekommt den angemeldeten Besitzer - eine fremde behält ihren, und
        /// eine gemeinsame wird nicht im Vorbeigehen vereinnahmt.
        /// </summary>
        [TestMethod]
        public void ClaimingTakesOnlyWhatHasNoOwner()
        {
            Session.SignIn(anna);

            var neu = Frage(null);
            QuestionOwnership.ClaimIfUnowned(neu);

            Assert.AreEqual(anna.Id, neu.OwnerPlayerId, "Die neue Frage bekam keinen Besitzer.");

            var fremde = Frage(bert.Id);
            QuestionOwnership.ClaimIfUnowned(fremde);

            Assert.AreEqual(bert.Id, fremde.OwnerPlayerId,
                "Eine fremde Frage hat den Besitzer gewechselt.");
        }
    }
}
