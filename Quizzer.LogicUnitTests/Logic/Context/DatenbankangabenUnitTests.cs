using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Logic.Context;

namespace Quizzer.LogicUnitTests.Logic.Context
{
    /// <summary>
    /// Die Verbindungszeichenfolge in Einzelfeldern.
    /// <para>
    /// <b>Nutzerwunsch vom 2026-09-06 abends:</b> „datenbank einstellungen aufdröseln und einzeln
    /// eingeben ... connectionstring wird zusammengebaut".
    /// </para>
    /// <para>
    /// <b>Die erste Zusicherung ist die teure.</b> Wer aus sechs Feldern eine neue Zeichenfolge
    /// baut, wirft alles Übrige weg - und das fällt niemandem auf, weil die Verbindung trotzdem
    /// steht: nur eben ohne die Zeitüberschreitung, die jemand aus gutem Grund hochgesetzt hatte.
    /// </para>
    /// </summary>
    [TestClass]
    public class DatenbankangabenUnitTests
    {
        private const string Bestand =
            @"Data Source=(localdb)\MSSQLLocalDB;Database=Quizzer;Integrated Security=True;"
            + "Connect Timeout=42;Application Name=Quizzer";

        /// <summary>Was die Maske nicht anfasst, bleibt stehen.</summary>
        [TestMethod]
        public void UnrelatedKeywordsSurviveTheRoundTrip()
        {
            var angaben = Datenbankangaben.Zerlege(Bestand);

            Assert.AreEqual(@"(localdb)\MSSQLLocalDB", angaben.Server);
            Assert.AreEqual("Quizzer", angaben.Datenbank);
            Assert.IsTrue(angaben.Windowsanmeldung);
            Assert.IsFalse(angaben.Unlesbar);

            angaben.Server = @"SRV01\SQL";

            StringAssert.Contains(angaben.Verbindung, "Connect Timeout=42",
                "Die Zeitueberschreitung ging beim Zusammenbauen verloren.");

            StringAssert.Contains(angaben.Verbindung, "Application Name=Quizzer",
                "Der Anwendungsname ging beim Zusammenbauen verloren.");

            StringAssert.Contains(angaben.Verbindung, @"Data Source=SRV01\SQL",
                "Der geaenderte Server steht nicht in der Zeichenfolge.");
        }

        /// <summary>
        /// Der Wechsel zurück auf die Windows-Anmeldung <b>entfernt</b> Benutzer und Kennwort.
        /// Sonst bliebe ein Kennwort im Klartext in der Einstellungsdatei stehen, das gar nicht
        /// mehr gebraucht wird.
        /// </summary>
        [TestMethod]
        public void SwitchingBackToWindowsLoginDropsTheCredentials()
        {
            var angaben = Datenbankangaben.Zerlege(Bestand);

            angaben.Windowsanmeldung = false;
            angaben.Benutzer = "quiz";
            angaben.Kennwort = "geheim";

            StringAssert.Contains(angaben.Verbindung, "User ID=quiz");
            StringAssert.Contains(angaben.Verbindung, "Password=geheim");

            angaben.Windowsanmeldung = true;

            Assert.IsFalse(angaben.Verbindung.Contains("geheim", StringComparison.Ordinal),
                "Das Kennwort steht nach dem Umschalten immer noch in der Zeichenfolge: "
                + angaben.Verbindung);

            Assert.IsFalse(angaben.Verbindung.Contains("User ID", StringComparison.Ordinal),
                "Der Benutzer steht nach dem Umschalten immer noch in der Zeichenfolge: "
                + angaben.Verbindung);

            Assert.AreEqual(string.Empty, angaben.Kennwort);
            Assert.AreEqual(string.Empty, angaben.Benutzer);
        }

        /// <summary>
        /// Zum Anzeigen wird das Kennwort unkenntlich, zum Speichern nicht. <b>Beides an einer
        /// Zusicherung</b>, weil ein Vertauschen der beiden Wege genau hier auffiele: entweder
        /// steht das Kennwort auf dem Bildschirm oder es kommt nie in der Datenbank an.
        /// </summary>
        [TestMethod]
        public void OnlyTheDisplayedStringHidesThePassword()
        {
            var angaben = Datenbankangaben.Zerlege(Bestand);

            angaben.Windowsanmeldung = false;
            angaben.Benutzer = "quiz";
            angaben.Kennwort = "geheim";

            StringAssert.Contains(angaben.Verbindung, "Password=geheim",
                "Die gespeicherte Zeichenfolge traegt das Kennwort nicht - die Verbindung "
                + "koennte nie zustande kommen.");

            Assert.IsFalse(
                angaben.VerbindungZumAnzeigen.Contains("geheim", StringComparison.Ordinal),
                "Die angezeigte Zeichenfolge zeigt das Kennwort im Klartext: "
                + angaben.VerbindungZumAnzeigen);

            StringAssert.Contains(angaben.VerbindungZumAnzeigen, "User ID=quiz",
                "Der Benutzer soll sichtbar bleiben - nur das Kennwort nicht.");
        }

        /// <summary>
        /// Eine unlesbare Verbindung ergibt die Vorgaben und sagt es. <b>Ohne diesen Weg wäre die
        /// Maske selbst kaputt</b> - und sie ist der einzige Weg zurück, wenn die Verbindung
        /// nicht stimmt.
        /// </summary>
        [TestMethod]
        public void AnUnreadableStringFallsBackAndSaysSo()
        {
            var kaputt = Datenbankangaben.Zerlege("das ist keine Verbindung");

            Assert.IsTrue(kaputt.Unlesbar,
                "Eine unlesbare Zeichenfolge wird stillschweigend durch die Vorgaben ersetzt.");

            Assert.AreEqual(Datenbankangaben.VorgabeServer, kaputt.Server);
            Assert.AreEqual(Datenbankangaben.VorgabeDatenbank, kaputt.Datenbank);

            var leer = Datenbankangaben.Zerlege(null);

            Assert.IsFalse(leer.Unlesbar,
                "Eine fehlende Einstellung ist kein Fehler - beim ersten Start ist das der "
                + "Normalfall.");

            Assert.AreEqual(Datenbankangaben.VorgabeServer, leer.Server);
        }
    }
}
