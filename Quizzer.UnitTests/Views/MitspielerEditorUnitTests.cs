using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views;
using System.IO;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Der Mitspieler-Editor: was er annimmt und was nicht.
    /// </summary>
    [TestClass]
    public class MitspielerEditorUnitTests
    {
        private RecordingUserPrompt prompt = null!;
        private readonly List<Guid> angelegt = new();

        [TestInitialize]
        public void SetUp()
        {
            prompt = new RecordingUserPrompt(answer: true);
            UserPrompt.Current = prompt;
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

            UserPrompt.Reset();
        }

        /// <summary>
        /// <b>Ein Mitspieler ohne Namen wird nicht gespeichert.</b>
        /// <para>
        /// <b>Gemessen 2026-09-07:</b> er liess sich anlegen und stand danach als leere Zeile in
        /// der Liste - und weil leer alphabetisch zuerst kommt, <b>vorgewählt in der
        /// Anmeldung</b>. Wer dann auf „Anmelden" drückt, spielt als Niemand.
        /// </para>
        /// </summary>
        [TestMethod]
        public async Task APlayerWithoutANameIsNotSaved()
        {
            var vm = new EditPlayerViewModel
            {
                Player = new Player { Id = Guid.NewGuid(), Designation = "", DisplayName = "" },
            };

            angelegt.Add(vm.Player.Id);

            await vm.VMSaveAsync();

            Assert.AreEqual(EditResultState.None, vm.ResultState,
                "Der namenlose Mitspieler wurde gespeichert.");

            Assert.AreEqual(1, prompt.Informs.Count, "Es kam keine Meldung.");

            StringAssert.Contains(prompt.Informs[0].Message, "Namen",
                "Die Meldung sagt nicht, was fehlt: " + prompt.Informs[0].Message);

            using var ctrl = new PlayersController();

            Assert.IsNull(await ctrl.GetAsync(vm.Player.Id),
                "Der namenlose Mitspieler steht in der Datenbank.");
        }

        /// <summary>
        /// <b>Die Gegenrichtung.</b> Ein Anzeigename allein genügt - <c>Designation</c> darf leer
        /// bleiben, sonst wäre die Maske nicht mehr zu bedienen.
        /// </summary>
        [TestMethod]
        public async Task ADisplayNameAloneIsEnough()
        {
            var vm = new EditPlayerViewModel
            {
                Player = new Player { Id = Guid.NewGuid(), Designation = "", DisplayName = "Anna" },
            };

            angelegt.Add(vm.Player.Id);

            await vm.VMSaveAsync();

            Assert.AreEqual(EditResultState.New, vm.ResultState,
                "Ein Mitspieler mit Anzeigenamen liess sich nicht speichern: "
                + string.Join(" | ", prompt.Informs.Select(i => i.Message)));
        }

        /// <summary>
        /// Der Bildwähler nimmt nur Bilder - geprüft an der Endung, <b>vor</b> dem Kopieren.
        /// <para>
        /// Eine Nicht-Bilddatei wurde bis 2026-09-07 erst in den Datenordner kopiert und danach
        /// abgewiesen; sie blieb als Leiche liegen. Eine unbekannte Endung liess
        /// <c>DetectResourceType</c> werfen - ein Fehlerfenster mit Stapelspur statt der
        /// vorgesehenen Meldung.
        /// </para>
        /// </summary>
        [TestMethod]
        public void OnlyImagesAreAcceptedAsAPlayerPicture()
        {
            foreach (var endung in new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" })
            {
                Assert.IsTrue(EditPlayerViewModel.IstBild(Path.Combine(Path.GetTempPath(), "p" + endung)),
                    $"{endung} wird abgewiesen, obwohl der Dateidialog es anbietet.");
            }

            foreach (var endung in new[] { ".mp4", ".pdf", ".txt", ".xyz", "" })
            {
                Assert.IsFalse(EditPlayerViewModel.IstBild(Path.Combine(Path.GetTempPath(), "p" + endung)),
                    $"'{endung}' gilt als Bild - die Datei landet im Datenordner und wird "
                    + "danach abgewiesen, oder die Erkennung wirft.");
            }
        }
    }
}
