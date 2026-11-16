using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.Server;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Quizzer.UnitTests.Server
{
    /// <summary>
    /// Aktualisierung des WPF (M36 A7) als reine Funktionen: Versionsvergleich, Entwicklungsbau,
    /// Startentscheidung, Pruefsumme vor dem Entpacken, kein Pfad aus der ZIP heraus.
    /// </summary>
    [TestClass]
    public class ClientUpdateUnitTests
    {
        internal const string Older = "20261101-1200-aaaaaaa";
        internal const string Middle = "20261110-0800-ccccccc";
        internal const string Newer = "20261116-1530-bbbbbbb";
        internal const string DevelopmentBuild = "1.0.0+c436c70c1acb1879f3e17b901ddbfba0f33e2792";

        private string folder = string.Empty;

        private string Root => Path.Combine(folder, "app");

        [TestInitialize]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "quizzer-update-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
        }

        [TestCleanup]
        public void TearDown()
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }

        /// <summary>Eine ZIP im Speicher; Eintragsnamen werden ungeprueft uebernommen - auch boese.</summary>
        internal static byte[] Zip(params string[] entries)
        {
            using var memory = new MemoryStream();
            using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var name in entries)
                {
                    using var writer = new StreamWriter(archive.CreateEntry(name).Open());
                    writer.Write("Inhalt von " + name);
                }
            }

            return memory.ToArray();
        }

        internal static ClientPackageInfo InfoFor(string version, byte[] bytes)
            => new(version, bytes.LongLength, Convert.ToHexString(SHA256.HashData(bytes)));

        private Task<string> InstallAsync(ClientPackageInfo info, byte[] bytes)
            => ClientUpdate.InstallAsync(info, (target, ct) => target.WriteAsync(bytes, ct).AsTask(), Root);

        private string Installed(string version, bool complete = true)
        {
            var target = Path.Combine(Root, version);
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, ClientUpdate.ExeName), "exe");
            if (complete)
                File.WriteAllText(Path.Combine(target, ClientUpdate.CompleteMarker), "ok");
            return Path.Combine(target, ClientUpdate.ExeName);
        }

        [TestMethod]
        [DataRow(Newer, Older, true, DisplayName = "neuer")]
        [DataRow(Older, Newer, false, DisplayName = "aelter")]
        [DataRow(Newer, Newer, false, DisplayName = "gleich")]
        [DataRow(Newer, Older + "+aaaaaaa1111", true, DisplayName = "SDK-Zusatz am laufenden")]
        [DataRow(Newer + "+x", Older, false, DisplayName = "Zusatz am angebotenen")]
        [DataRow("20261116-1530-BBBBBBB", Older, false, DisplayName = "Grossbuchstaben")]
        [DataRow("20261332-1530-bbbbbbb", Older, false, DisplayName = "kein Datum")]
        [DataRow("..\\..\\20261116-1530-bbbbbbb", Older, false, DisplayName = "Pfad")]
        [DataRow("20261116-1530-bbbbbbb\n", Older, false, DisplayName = "Zeilenende")]
        public void OnlyANewerCanonicalPackageIsAnUpdate(string offered, string running, bool expected)
        {
            Assert.AreEqual(expected, ClientUpdate.IsOffered(new ClientPackageInfo(offered, 1, "00"), running));
        }

        [TestMethod]
        [DataRow(DevelopmentBuild)]
        [DataRow("1.0.0")]
        [DataRow("")]
        [DataRow(null)]
        public void ADevelopmentBuildIsNeverOfferedAnUpdate(string? running)
        {
            Assert.IsNull(PackageVersion.TryParse(running));
            Assert.IsFalse(ClientUpdate.IsOffered(new ClientPackageInfo(Newer, 1, "00"), running));
        }

        [TestMethod]
        public void NoPackageNoOffer()
        {
            Assert.IsFalse(ClientUpdate.IsOffered(null, Older));
        }

        /// <summary>Die Verknuepfung des Freundes zeigt auf die erste Installation - die startet die neueste vollstaendige.</summary>
        [TestMethod]
        public void AtStartTheNewestCompleteNewerVersionTakesOver()
        {
            Installed(Older);
            var expected = Installed(Middle);
            Installed(Newer, complete: false);
            Installed("20991231-2359-ABCDEF0");
            File.WriteAllText(Path.Combine(Root, "20991231-2359-eeeeeee"), "eine Datei, kein Ordner");

            Assert.AreEqual(expected, ClientUpdate.FindNewerInstalled(Root, Older));
        }

        [TestMethod]
        public void AtStartNothingHappensWhenTheRunningVersionIsTheNewest()
        {
            Installed(Older);
            Installed(Middle);

            Assert.IsNull(ClientUpdate.FindNewerInstalled(Root, Middle));
            Assert.IsNull(ClientUpdate.FindNewerInstalled(Path.Combine(folder, "gibt-es-nicht"), Older));
        }

        [TestMethod]
        [DataRow(DevelopmentBuild)]
        [DataRow(null)]
        public void ADevelopmentBuildNeverHandsOverAtStart(string? running)
        {
            Installed(Newer);

            Assert.IsNull(ClientUpdate.FindNewerInstalled(Root, running));
        }

        [TestMethod]
        public async Task AnInstalledPackageIsCompleteAndTakesOverAtTheNextStart()
        {
            var bytes = Zip(ClientUpdate.ExeName, "runtimes/win-x64/native/a.dll", "de/Quizzer.resources.dll");

            var exe = await InstallAsync(InfoFor(Newer, bytes), bytes);

            Assert.AreEqual(Path.Combine(Root, Newer, ClientUpdate.ExeName), exe);
            Assert.IsTrue(File.Exists(Path.Combine(Root, Newer, "runtimes", "win-x64", "native", "a.dll")));
            Assert.AreEqual(exe, ClientUpdate.FindNewerInstalled(Root, Older));
            CollectionAssert.AreEquivalent(new[] { Newer }, Directory.EnumerateFileSystemEntries(Root).Select(Path.GetFileName).ToArray(),
                "Download- oder Zwischenordner blieben liegen.");
        }

        /// <summary>Ein Eintrag wie <c>..\x</c> darf nichts ausserhalb des Zielordners schreiben.</summary>
        [TestMethod]
        [DataRow("../ausbruch.txt")]
        [DataRow("unter/../../ausbruch.txt")]
        [DataRow("..\\ausbruch.txt")]
        public async Task AnEntryOutsideTheTargetFolderStopsTheInstallation(string evil)
        {
            var bytes = Zip(ClientUpdate.ExeName, evil);

            var ex = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => InstallAsync(InfoFor(Newer, bytes), bytes));

            Assert.AreEqual(ClientUpdate.PathEscape, ex.Message);
            Assert.IsFalse(File.Exists(Path.Combine(Root, "ausbruch.txt")), "Die Datei ist aus dem Zielordner ausgebrochen.");
            Assert.IsFalse(Directory.Exists(Path.Combine(Root, Newer)));
            Assert.IsNull(ClientUpdate.FindNewerInstalled(Root, Older));
        }

        [TestMethod]
        public async Task AWrongChecksumStopsBeforeAnythingIsUnpacked()
        {
            var bytes = Zip(ClientUpdate.ExeName);
            var other = Zip(ClientUpdate.ExeName, "anders.txt");

            var ex = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => InstallAsync(InfoFor(Newer, other) with { Size = bytes.LongLength }, bytes));

            Assert.AreEqual(ClientUpdate.ChecksumMismatch, ex.Message);
            Assert.AreEqual(0, Directory.EnumerateFileSystemEntries(Root).Count(), "Nach dem Abbruch blieb etwas liegen.");
        }

        /// <summary>
        /// Geprueft wird <b>vor</b> dem Entpacken: eine kaputte Datei scheitert an der Pruefsumme, nicht erst
        /// am ZIP-Leser - sonst laege das Entpacken einer fremden Datei schon hinter uns.
        /// </summary>
        [TestMethod]
        public async Task TheChecksumIsCheckedBeforeTheArchiveIsOpened()
        {
            var bytes = RandomNumberGenerator.GetBytes(500);
            var info = InfoFor(Newer, bytes) with { Sha256 = new string('0', 64) };

            var ex = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => InstallAsync(info, bytes));

            Assert.AreEqual(ClientUpdate.ChecksumMismatch, ex.Message);
        }

        [TestMethod]
        public async Task APackageWithoutTheProgramIsNotInstalled()
        {
            var bytes = Zip("liesmich.txt");

            var ex = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => InstallAsync(InfoFor(Newer, bytes), bytes));

            Assert.AreEqual(ClientUpdate.NoProgram, ex.Message);
            Assert.IsFalse(Directory.Exists(Path.Combine(Root, Newer)));
        }

        /// <summary>Die Version wird zum Ordnernamen - eine vom Server erfundene darf keinen Pfad bilden.</summary>
        [TestMethod]
        [DataRow("../20261116-1530-bbbbbbb")]
        [DataRow("..\\entkommen")]
        public async Task AVersionThatIsNoVersionNamesNoFolder(string version)
        {
            var bytes = Zip(ClientUpdate.ExeName);

            var ex = await Assert.ThrowsExactlyAsync<InvalidDataException>(() => InstallAsync(InfoFor(version, bytes), bytes));

            Assert.AreEqual(ClientUpdate.InvalidVersion, ex.Message);
            var outside = Directory.EnumerateFileSystemEntries(folder).Select(Path.GetFileName).Where(n => n != "app").ToList();
            Assert.AreEqual(0, outside.Count, "Neben dem Versionsordner entstand: " + string.Join(", ", outside));
        }
    }
}
