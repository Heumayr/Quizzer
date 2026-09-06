using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels;

namespace Quizzer.UnitTests
{
    /// <summary>
    /// Der Programmstart zieht die Datenbank auf den Stand der Migrationen.
    /// <para>
    /// <b>Nutzerentscheidung vom 2026-09-06 (Frage F03).</b> <c>EnsureMigrated</c> gab es vorher
    /// schon - mit dem Kommentar „Gefahrlos und deshalb oeffentlich" - und <b>hatte keinen
    /// einzigen Aufrufer</b>. Gefunden hat das der Vollstaendigkeitskritiker eines Rueckfragen-
    /// Laufs, nicht der Uebersetzer: eine oeffentliche Methode ohne Aufrufer ist keine Warnung.
    /// </para>
    /// <para>
    /// Die Folge traf den Rueckweg des Spielleiters. Eine Sicherung liegt naturgemaess vor der
    /// juengsten Migration; spielt er sie zurueck, fehlen Spalten, und ohne diesen Aufruf haette
    /// er <c>dotnet ef database update</c> von der Kommandozeile gebraucht.
    /// </para>
    /// <para>
    /// <b>Der Test misst beide Richtungen</b> - dass es mit gueltiger Verbindung durchlaeuft, und
    /// dass es mit unbrauchbarer Verbindung <c>false</c> meldet statt zu werfen. Ohne die zweite
    /// waere ungeprueft, ob die Absicherung ueberhaupt greift.
    /// </para>
    /// <para>
    /// <b>Mutationsprobe am 2026-09-06:</b> mit ausgebautem <c>EnsureMigrated</c>-Aufruf faellt
    /// die zweite Zusicherung, die erste bleibt gruen. Sie allein wuerde also nichts messen.
    /// </para>
    /// <para>
    /// <b>Was diese Klasse nicht abdeckt, und zwar bewusst:</b> ob <c>App.OnStartup</c> die
    /// Methode ueberhaupt <i>aufruft</i>. Wer die eine Zeile dort entfernt, laesst diese Tests
    /// gruen - genau der Zustand, aus dem der Befund kam. Das zu messen braucht eine gestartete
    /// <c>Application</c>, und die gibt es im Testlauf nicht.
    /// </para>
    /// </summary>
    [TestClass]
    public class StartupMigrationUnitTests
    {
        private string original = string.Empty;
        private RecordingUserPrompt prompt = null!;

        [TestInitialize]
        public void SetUp()
        {
            original = Settings.ConnectionString;

            prompt = new RecordingUserPrompt(answer: true);
            UserPrompt.Current = prompt;
        }

        [TestCleanup]
        public void TearDown()
        {
            Settings.ConnectionString = original;
            UserPrompt.Reset();
        }

        /// <summary>
        /// Mit der Verbindung des Testlaufs laeuft der Startschritt durch und meldet nichts.
        /// </summary>
        [TestMethod]
        public void WithAWorkingConnectionTheStartupMigrationSucceeds()
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(Settings.ConnectionString),
                "Ohne Verbindungszeichenfolge misst dieser Test gar nichts.");

            var ergebnis = App.DatenbankAufStandBringen();

            Assert.IsTrue(ergebnis,
                "Der Startschritt meldet einen Fehlschlag, obwohl die Testdatenbank erreichbar ist.");

            Assert.AreEqual(0, prompt.Informs.Count,
                "Es wurde ein Hinweisfenster gezeigt, obwohl nichts schiefgegangen ist: "
                + string.Join(" | ", prompt.Informs.Select(i => i.Caption)));
        }

        /// <summary>
        /// Die Gegenrichtung: eine unbrauchbare Verbindung meldet <c>false</c> und sagt es dem
        /// Spielleiter - sie wirft nicht.
        /// <para>
        /// Ohne diese Probe waere die Zusicherung oben auch dann gruen, wenn der
        /// <c>catch</c>-Block gar nicht erreichbar waere.
        /// </para>
        /// </summary>
        [TestMethod]
        public void WithABrokenConnectionItReportsInsteadOfThrowing()
        {
            Settings.ConnectionString =
                "Data Source=(localdb)\\GibtEsNicht;Database=GibtEsAuchNicht;"
                + "Integrated Security=True;Connect Timeout=2";

            var ergebnis = App.DatenbankAufStandBringen();

            Assert.IsFalse(ergebnis,
                "Eine unerreichbare Datenbank wurde als Erfolg gemeldet. Dann startet das "
                + "Programm mit halbem Schema weiter.");

            Assert.AreEqual(1, prompt.Informs.Count,
                "Der Spielleiter erfaehrt nichts davon, dass die Datenbank nicht erreichbar ist.");

            Assert.IsTrue(prompt.Informs[0].Message.Contains("appsettings.json"),
                "Die Meldung sagt nicht, wo die Verbindungszeichenfolge steht. Gelesen wurde: "
                + prompt.Informs[0].Message);
        }
    }
}
