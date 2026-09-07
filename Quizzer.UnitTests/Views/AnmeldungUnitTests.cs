using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views;
using System.Windows;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Die Anmeldung beim Start — das Erste, was das Programm tut.
    /// <para>
    /// <b>Sie hatte bis 2026-09-07 keine einzige Zusicherung</b>, obwohl an ihr hängt, ob das
    /// Programm überhaupt aufgeht: <c>App.Anmelden</c> beendet sich, wenn niemand angemeldet
    /// ist — <b>außer</b> es gibt gar keinen Spielleiter. Ohne diese Ausnahme hätte sich der
    /// Nutzer nach dem Einspielen der Anmeldung ausgesperrt, denn den Haken „darf leiten" setzt
    /// man nur <i>im</i> Programm. Der Kommentar dazu stand im Code; gemessen hat es niemand.
    /// </para>
    /// </summary>
    [TestClass]
    public class AnmeldungUnitTests
    {
        private readonly List<Guid> angelegt = new();

        [TestInitialize]
        public void SetUp()
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);

            Session.SignOut();
        }

        [TestCleanup]
        public async Task TearDown()
        {
            using (var ctrl = new PlayersController())
            {
                foreach (var id in angelegt)
                    await ctrl.DeleteAsync(id);

                await ctrl.SaveChangesAsync();
            }

            Session.SignOut();
            UserPrompt.Reset();
        }

        /// <summary>Legt einen Mitspieler an und merkt ihn zum Aufräumen vor.</summary>
        private async Task<Player> AnlegenAsync(string name, bool moderator, string? kennwort = null)
        {
            using var ctrl = new PlayersController();

            var spieler = new Player
            {
                Id = Guid.NewGuid(),
                Designation = name,
                IsModerator = moderator,
                PasswordHash = kennwort == null ? string.Empty : Session.HashPassword(kennwort),
            };

            var eingefuegt = await ctrl.InsertAsync(spieler);

            await ctrl.SaveChangesAsync();

            angelegt.Add(eingefuegt.Entity.Id);

            return eingefuegt.Entity;
        }

        private static async Task<LoginViewModel> GeladenAsync()
        {
            var vm = new LoginViewModel();

            await vm.LoadForTestAsync();

            return vm;
        }

        /// <summary>
        /// <b>Nur wer leiten darf, steht zur Wahl.</b> Stünden alle da, wählte jemand
        /// versehentlich einen Gast und sähe dessen Fragenbestand.
        /// </summary>
        [TestMethod]
        public async Task OnlyModeratorsAreOffered()
        {
            var leiter = await AnlegenAsync("Zzz Leiter " + Guid.NewGuid().ToString("N")[..6], moderator: true);
            var gast = await AnlegenAsync("Zzz Gast " + Guid.NewGuid().ToString("N")[..6], moderator: false);

            var vm = await GeladenAsync();

            CollectionAssert.Contains(vm.Moderators.Select(m => m.Id).ToArray(), leiter.Id,
                "Ein Spielleiter fehlt in der Auswahl.");

            CollectionAssert.DoesNotContain(vm.Moderators.Select(m => m.Id).ToArray(), gast.Id,
                "Ein Mitspieler ohne Leitungsrecht steht zur Wahl.");
        }

        /// <summary>
        /// <b>Die Aussperr-Sicherung.</b> Gibt es keinen Spielleiter, meldet das ViewModel das —
        /// und <c>App.Anmelden</c> lässt das Programm dann trotzdem weiterlaufen.
        /// </summary>
        [TestMethod]
        public async Task WithoutAnyModeratorTheMaskSaysSo()
        {
            // Alle vorhandenen Leiter kurz entziehen, damit die Lage wirklich eintritt.
            var zurueck = new List<Player>();

            using (var ctrl = new PlayersController())
            {
                foreach (var p in (await ctrl.GetAllAsync()).Where(p => p.IsModerator))
                {
                    p.IsModerator = false;
                    zurueck.Add(p);

                    await ctrl.UpdateAsync(p);
                }

                await ctrl.SaveChangesAsync();
            }

            try
            {
                var vm = await GeladenAsync();

                Assert.IsFalse(vm.HasModerators,
                    "Es wurde ein Spielleiter gefunden, obwohl keiner das Recht hat - dann misst "
                    + "diese Zusicherung nichts.");

                Assert.AreEqual(Visibility.Visible, vm.EmptyHintVisibility,
                    "Die Maske stuende leer da, ohne zu sagen, was zu tun ist.");
            }
            finally
            {
                using var ctrl = new PlayersController();

                foreach (var p in zurueck)
                {
                    p.IsModerator = true;
                    await ctrl.UpdateAsync(p);
                }

                await ctrl.SaveChangesAsync();
            }
        }

        /// <summary>
        /// <b>Ein falsches Kennwort meldet sich nicht an</b> - und sagt warum, statt das Fenster
        /// wortlos offen zu lassen.
        /// </summary>
        [TestMethod]
        public async Task AWrongPasswordIsRefusedWithAReason()
        {
            var leiter = await AnlegenAsync(
                "Zzz Kennwort " + Guid.NewGuid().ToString("N")[..6], moderator: true, kennwort: "geheim");

            var vm = await GeladenAsync();

            vm.SelectedModerator = vm.Moderators.First(m => m.Id == leiter.Id);

            Assert.AreEqual(Visibility.Visible, vm.PasswordVisibility,
                "Das Kennwortfeld erscheint nicht, obwohl eines gesetzt ist.");

            vm.Password = "falsch";

            vm.SignInCommand.Execute(null);

            Assert.IsFalse(vm.SignedIn, "Mit falschem Kennwort wurde angemeldet.");
            Assert.IsFalse(Session.IsSignedIn, "Die Sitzung haelt jemanden fuer angemeldet.");

            StringAssert.Contains(vm.ErrorText, "Kennwort",
                "Es steht kein Grund da: " + vm.ErrorText);

            // Die Gegenrichtung: mit dem richtigen Kennwort geht es. Ohne sie waere die
            // Zusicherung auch gruen, wenn die Anmeldung IMMER scheiterte.
            vm.Password = "geheim";

            vm.SignInCommand.Execute(null);

            Assert.IsTrue(vm.SignedIn, "Mit richtigem Kennwort kam die Anmeldung nicht zustande.");
            Assert.AreEqual(leiter.Id, Session.CurrentModeratorId);
        }

        /// <summary>
        /// <b>Beim Wechsel steht der bisher Angemeldete vorgewählt</b> - sonst übernähme
        /// stillschweigend jemand anderes, nur weil er alphabetisch vorn steht.
        /// </summary>
        [TestMethod]
        public async Task ASwitchPreselectsWhoeverIsSignedIn()
        {
            var a = await AnlegenAsync("Aaa Erst " + Guid.NewGuid().ToString("N")[..6], moderator: true);
            var z = await AnlegenAsync("Zzz Spaet " + Guid.NewGuid().ToString("N")[..6], moderator: true);

            Session.SignIn(z);

            var vm = await GeladenAsync();

            Assert.AreEqual(z.Id, vm.SelectedModerator?.Id,
                "Vorgewaehlt ist nicht der Angemeldete, sondern jemand anderes - beim Wechseln "
                + "uebernaehme er unbemerkt.");

            Assert.AreNotEqual(a.Id, vm.SelectedModerator?.Id);
        }

        /// <summary>Der Abbruch-Knopf heißt beim Start anders als beim Wechsel.</summary>
        [TestMethod]
        public async Task TheCancelButtonIsNamedForTheSituation()
        {
            var vm = await GeladenAsync();

            Assert.AreEqual("Beenden", vm.AbbruchText,
                "Beim Start heisst Abbrechen: das Programm nicht starten.");

            vm.IstWechsel = true;

            Assert.AreEqual("Abbrechen", vm.AbbruchText,
                "Beim Wechsel waere Beenden falsch - das Programm laeuft weiter.");

            StringAssert.Contains(vm.Untertitel, "übernimmt",
                "Die Zeile unter dem Titel passt nicht zum Wechsel: " + vm.Untertitel);
        }
    }
}
