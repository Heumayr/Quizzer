using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Transfer.Server;

namespace Quizzer.LogicUnitTests.DataModels
{
    /// <summary>
    /// Wohin das WPF im Servermodus die abgeholten Medien legt.
    /// <para>
    /// <b>Nutzerwort vom 2026-10-23 (M26):</b> „die datein die geladen werden von der api ... in
    /// den programdata folder landen per default". Vorher landeten sie im Datenordner des
    /// Einzelbetriebs, neben den eigenen Dateien.
    /// </para>
    /// </summary>
    [TestClass]
    public class ServerMediaFolderUnitTests
    {
        /// <summary>Unter ProgramData, je Server ein Ordner aus Name und Port.</summary>
        [TestMethod]
        public void EachServerGetsItsOwnFolderUnderProgramData()
        {
            Assert.AreEqual(@"C:\PD\Quizzer\Server\localhost_8543",
                ServerMediaFolder.For(new Uri("https://localhost:8543/api/v1/"), @"C:\PD"),
                "Der Pfad der Adresse gehört nicht in den Ordnernamen, Name und Port schon.");

            Assert.AreEqual(@"C:\PD\Quizzer\Server\quiz-pc.tail1.ts.net_8543",
                ServerMediaFolder.For(new Uri("https://Quiz-PC.tail1.ts.net:8543"), @"C:\PD"),
                "Groß geschriebene Namen ergäben für denselben Server einen zweiten Ordner.");

            Assert.AreNotEqual(
                ServerMediaFolder.For(new Uri("https://localhost:8543"), @"C:\PD"),
                ServerMediaFolder.For(new Uri("https://localhost:8443"), @"C:\PD"),
                "Zwei Server auf einem Rechner teilten sich sonst einen Ordner - gleich benannte "
                + "Design-Ordner liefen ineinander.");
        }

        /// <summary>Ohne Angabe im Uri ist es der Port von https - er steht trotzdem im Namen.</summary>
        [TestMethod]
        public void TheDefaultPortIsNamedToo()
        {
            Assert.AreEqual(@"C:\PD\Quizzer\Server\server_443",
                ServerMediaFolder.For(new Uri("https://server/"), @"C:\PD"));
        }

        /// <summary>Eine IPv6-Adresse trägt Doppelpunkte, und die gehören in keinen Ordnernamen.</summary>
        [TestMethod]
        public void AnIpv6AddressStillGivesAValidFolderName()
        {
            var name = Path.GetFileName(ServerMediaFolder.For(new Uri("https://[::1]:8543/"), @"C:\PD"));

            Assert.AreEqual(-1, name.IndexOfAny(Path.GetInvalidFileNameChars()),
                "Ungültiges Zeichen im Ordnernamen: " + name);
            Assert.IsTrue(name.EndsWith("_8543", StringComparison.Ordinal), name);
        }

        /// <summary>Ohne Umlenkung ist es wirklich ProgramData - nicht das Profil des Benutzers.</summary>
        [TestMethod]
        public void WithoutAnOverrideItIsProgramData()
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            Assert.AreEqual(Path.Combine(programData, "Quizzer", "Server", "localhost_8543"),
                ServerMediaFolder.For(new Uri("https://localhost:8543")));
        }
    }
}
