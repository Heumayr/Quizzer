using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels;
using Quizzer.Logic.Transfer;

namespace Quizzer.LogicUnitTests.Logic
{
    /// <summary>
    /// Kein Schreibvorgang verlässt den Datenordner - auch nicht, wenn das Bündel es versucht.
    /// <para>
    /// <b>Ein importiertes Bündel ist fremdes Material.</b> Darin stehen Dateinamen, die jemand
    /// anders geschrieben hat. Würden sie als Pfad benutzt, genügte <c>..\..\Windows\etwas.txt</c>,
    /// um beim Auspacken irgendwohin zu schreiben - eine Datei, die man per E-Mail bekommt, wäre
    /// damit ein Werkzeug.
    /// </para>
    /// <para>
    /// <b>Deshalb bestimmt der Name im Bündel den Zielnamen gar nicht.</b> Er entsteht aus einem
    /// Prüfwert über den Inhalt. Der Ausbruchsversuch unten misst die zweite Absicherung: dass
    /// auch ein von Hand hineingereichter Name nicht aus der Wurzel führt.
    /// </para>
    /// </summary>
    [TestClass]
    public class MediaVaultUnitTests
    {
        private string sandkasten = string.Empty;
        private string datenordner = string.Empty;
        private string vorher = string.Empty;

        private readonly FileSystemMediaVault tresor = new();

        [TestInitialize]
        public void SetUp()
        {
            vorher = Settings.FilePathQuizzer;

            // Der Datenordner liegt tief in einem Sandkasten - nur so landet ein relativer
            // Ausbruch noch darin und wird damit ueberhaupt messbar.
            sandkasten = Path.Combine(Path.GetTempPath(), "quizzer-sandkasten-" + Guid.NewGuid());
            datenordner = Path.Combine(sandkasten, "eine", "zwei", "daten");

            Directory.CreateDirectory(datenordner);

            Settings.FilePathQuizzer = datenordner;
        }

        [TestCleanup]
        public void TearDown()
        {
            Settings.FilePathQuizzer = vorher;

            if (Directory.Exists(sandkasten))
                Directory.Delete(sandkasten, recursive: true);
        }

        /// <summary>
        /// Alles, was im Sandkasten liegt, aber nicht im Datenordner - also jeder Ausbruch.
        /// <para>
        /// <b>Gemessen wird der Eintrag, nicht ein erwarteter Dateiname.</b> Beim ersten Anlauf
        /// suchte diese Probe nach einer Datei namens <c>ausbruch.txt</c> - und blieb grün,
        /// obwohl <b>beide</b> Riegel ausgebaut waren: der Ausbruch legt einen <b>Ordner</b>
        /// dieses Namens an, und die Datei darin heißt nach ihrem Prüfwert.
        /// </para>
        /// </summary>
        private List<string> WasAusgebrochenIst()
        {
            var daten = Path.GetFullPath(datenordner);

            return Directory
                .GetFileSystemEntries(sandkasten, "*", SearchOption.AllDirectories)
                .Select(Path.GetFullPath)
                .Where(e => !e.StartsWith(daten, StringComparison.OrdinalIgnoreCase))
                .Where(e => !daten.StartsWith(e + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>Geschrieben, gelesen, und der Name kommt vom Tresor.</summary>
        [TestMethod]
        public void WhatIsWrittenCanBeReadBack()
        {
            var inhalt = System.Text.Encoding.UTF8.GetBytes("ein Bild, angeblich");

            var name = tresor.Write(MediaRoot.Resources, null, ".png", inhalt);

            Assert.IsTrue(name.EndsWith(".png", StringComparison.Ordinal),
                "Die Endung ging verloren: " + name);

            CollectionAssert.AreEqual(inhalt, tresor.Read(MediaRoot.Resources, null, name),
                "Was gelesen wird, ist nicht das, was geschrieben wurde.");
        }

        /// <summary>
        /// Derselbe Inhalt landet nur einmal auf der Platte - ein zweiter Import derselben Datei
        /// legt sie nicht noch einmal ab.
        /// </summary>
        [TestMethod]
        public void TheSameContentIsStoredOnlyOnce()
        {
            var inhalt = System.Text.Encoding.UTF8.GetBytes("derselbe Ton");

            var ersterName = tresor.Write(MediaRoot.Resources, null, ".mp3", inhalt);
            var zweiterName = tresor.Write(MediaRoot.Resources, null, ".mp3", inhalt);

            Assert.AreEqual(ersterName, zweiterName,
                "Zwei gleiche Inhalte bekamen zwei Namen - ein wiederholter Import fuellt damit "
                + "den Datenordner.");

            Assert.AreEqual(1, Directory.GetFiles(Settings.ResourceRootFolder).Length,
                "Es liegt mehr als eine Datei da.");
        }

        /// <summary>
        /// Unterschiedliche Inhalte bekommen unterschiedliche Namen. Die Gegenrichtung zur Probe
        /// darüber - ohne sie wäre sie auch dann grün, wenn <b>jede</b> Datei denselben Namen
        /// bekäme und sich gegenseitig überschriebe.
        /// </summary>
        [TestMethod]
        public void DifferentContentGetsDifferentNames()
        {
            var a = tresor.Write(MediaRoot.Resources, null, ".png",
                System.Text.Encoding.UTF8.GetBytes("Bild A"));
            var b = tresor.Write(MediaRoot.Resources, null, ".png",
                System.Text.Encoding.UTF8.GetBytes("Bild B"));

            Assert.AreNotEqual(a, b,
                "Zwei verschiedene Bilder bekamen denselben Namen - das zweite haette das erste "
                + "ueberschrieben.");
        }

        /// <summary>
        /// <b>Der Ausbruchsversuch.</b> Weder ein Dateiname noch ein Ordnername aus dem Bündel
        /// führt aus dem Datenordner heraus.
        /// </summary>
        [TestMethod]
        [DataRow(@"..\..\..\ausbruch")]
        [DataRow("../../../ausbruch-zwei")]
        [DataRow(@"..\..\..\..\ganz-raus")]
        public void NoNameEscapesTheDataFolder(string boesartig)
        {
            // Ueber den Ordnernamen eines Designs ...
            tresor.Write(MediaRoot.Theme, boesartig, ".png",
                System.Text.Encoding.UTF8.GetBytes("Textur"));

            // ... und ueber den Dateinamen beim Lesen.
            tresor.Read(MediaRoot.Resources, null, boesartig);
            tresor.Read(MediaRoot.Theme, boesartig, boesartig);

            var ausgebrochen = WasAusgebrochenIst();

            Assert.AreEqual(0, ausgebrochen.Count,
                "Es wurde ausserhalb des Datenordners geschrieben: "
                + string.Join(", ", ausgebrochen));
        }

        /// <summary>
        /// Auch ein <b>absoluter</b> Pfad im Bündel führt nicht dorthin, wohin er zeigt.
        /// <para>
        /// Getrennt von der Probe darüber, weil ein absoluter Pfad einen anderen Weg durch
        /// <c>Path.Combine</c> nimmt: er verwirft den bisherigen Anfang vollständig, statt ihn
        /// zu ergänzen.
        /// </para>
        /// </summary>
        [TestMethod]
        public void NotEvenAnAbsolutePathEscapes()
        {
            var ziel = Path.Combine(sandkasten, "absolut-daneben");

            tresor.Write(MediaRoot.Theme, ziel, ".png",
                System.Text.Encoding.UTF8.GetBytes("Textur"));

            tresor.Read(MediaRoot.Resources, null, Path.Combine(ziel, "egal.png"));

            var ausgebrochen = WasAusgebrochenIst();

            Assert.AreEqual(0, ausgebrochen.Count,
                "Ein absoluter Pfad aus dem Buendel hat bestimmt, wohin geschrieben wird: "
                + string.Join(", ", ausgebrochen));
        }

        /// <summary>
        /// Eine Endung, die das Programm gar nicht abspielt, wird nicht als solche abgelegt.
        /// <para>
        /// Sonst läge nach einem Import eine <c>.exe</c> im Datenordner, benannt wie eine
        /// Mediendatei.
        /// </para>
        /// </summary>
        [TestMethod]
        public void AnUnknownExtensionIsNotStoredAsSuch()
        {
            var name = tresor.Write(MediaRoot.Resources, null, ".exe",
                System.Text.Encoding.UTF8.GetBytes("MZ"));

            Assert.IsFalse(name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase),
                "Eine ausfuehrbare Datei wurde mit ihrer Endung abgelegt: " + name);

            Assert.IsTrue(name.EndsWith(".bin", StringComparison.Ordinal),
                "Erwartet war der Rueckfall auf .bin, gespeichert wurde: " + name);
        }
    }
}
