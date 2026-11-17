using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.RegularExpressions;

namespace Quizzer.LogicUnitTests.Standards
{
    /// <summary>
    /// Kein Test bindet über 127.0.0.1 hinaus (V14, Nutzermeldung 2026-11-16: „der testhost verlangt zugriff auf
    /// privates netzwerk"). Jeder <c>testhost.exe</c> aus einem neuen Arbeitsbaum ist für Windows ein neues
    /// Programm, und auf allen Adressen stünde der Testserver im Heimnetz. Den Buzzer-Server starten nur die
    /// beiden Helfer, und die binden an <c>TestNetwork.Address</c>.
    /// </summary>
    [TestClass]
    public class TestServerLoopbackUnitTests
    {
        private static readonly string[] Testprojekte = ["Quizzer.LogicUnitTests", "Quizzer.UnitTests", "Quizzer.Server.Tests"];

        /// <summary>Die einzigen Dateien, die einen Testserver starten.</summary>
        private static readonly string[] Helfer =
        [
            Path.Combine("Quizzer.LogicUnitTests", "LocalBuzzer", "HubTest.cs"),
            Path.Combine("Quizzer.UnitTests", "Views", "BuzzerViews", "BuzzerServerTestBasis.cs"),
        ];

        /// <summary>Was nur ein Helfer darf: Optionen bauen, Port oder Adresse setzen, starten, Kestrel einrichten.</summary>
        private static readonly Regex NurImHelfer = new(
            @"new\s+BuzzerServerOptions\b|\b(?:Buzzer)?BindAddress\s*=(?![=>])|\bBuzzerPort\s*=(?![=>])|\bStartServerCommand\b"
            + @"|\bUseUrls\s*\(|\bUseKestrel\b|\bConfigureKestrel\b");

        /// <summary>Alle Adressen in jeder Schreibweise, auch ein Listener, der nicht ausdrücklich Loopback nimmt.</summary>
        private static readonly Regex AlleAdressen = new(
            @"IPAddress\s*\.\s*(?:Any|IPv6Any)\b|\bListenAnyIP\b|\bTcpListener\s*\.\s*Create\s*\("
            + @"|https?://(?:\*|\+|0\.0\.0\.0|\[::\])[:/]"
            + @"|new\s+(?:System\.Net\.Sockets\.)?TcpListener\s*\((?!\s*IPAddress\s*\.\s*(?:Loopback|IPv6Loopback)\b)");

        /// <summary>Wer einen BuzzerServer anlegt, darf ihn nicht selbst starten - sonst gilt die Vorgabe 0.0.0.0.</summary>
        private static readonly Regex NeuerServer = new(@"(\w+)\s*=\s*new\s+(?:global::)?(?:LocalBuzzer\.Service\.)?BuzzerServer\b");

        private static string RepoRoot()
        {
            var verzeichnis = new DirectoryInfo(AppContext.BaseDirectory);

            while (verzeichnis != null && !File.Exists(Path.Combine(verzeichnis.FullName, "Quizzer.slnx")))
                verzeichnis = verzeichnis.Parent;

            Assert.IsNotNull(verzeichnis, "Quizzer.slnx nicht gefunden.");

            return verzeichnis.FullName;
        }

        private static int Zeile(string inhalt, int index) => inhalt.Take(index).Count(c => c == '\n') + 1;

        internal static List<string> Verstoesse(string datei, string inhalt, bool istHelfer)
        {
            var treffer = AlleAdressen.Matches(inhalt).ToList();

            if (!istHelfer)
            {
                treffer.AddRange(NurImHelfer.Matches(inhalt));

                foreach (var name in NeuerServer.Matches(inhalt).Select(m => m.Groups[1].Value).Distinct())
                    treffer.AddRange(Regex.Matches(inhalt, $@"\b{Regex.Escape(name)}\s*\??\.\s*StartAsync\s*\("));
            }

            return treffer.Select(m => $"{datei}:{Zeile(inhalt, m.Index)}: {m.Value.Trim()}").ToList();
        }

        [TestMethod]
        public void NoTestBindsBeyondLoopback()
        {
            var root = RepoRoot();
            var dateien = Testprojekte
                .SelectMany(p => Directory.EnumerateFiles(Path.Combine(root, p), "*.cs", SearchOption.AllDirectories))
                .Select(p => Path.GetRelativePath(root, p))
                .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                         && Path.GetFileName(p) != $"{nameof(TestServerLoopbackUnitTests)}.cs")
                .ToList();

            // Gegenrichtung: ein Suchlauf, der nichts liest, meldet sonst Erfolg.
            foreach (var projekt in Testprojekte)
                Assert.IsTrue(dateien.Count(d => d.StartsWith(projekt + Path.DirectorySeparatorChar)) > 10, $"{projekt}: kaum Dateien gelesen.");

            foreach (var helfer in Helfer)
                Assert.IsTrue(NurImHelfer.IsMatch(File.ReadAllText(Path.Combine(root, helfer))), $"{helfer} startet keinen Server mehr - greift das Muster noch?");

            var verstoesse = dateien
                .SelectMany(d => Verstoesse(d, File.ReadAllText(Path.Combine(root, d)), Helfer.Contains(d)))
                .ToList();

            Assert.AreEqual(0, verstoesse.Count,
                "Ein Test bindet über 127.0.0.1 hinaus oder startet den Buzzer-Server an den Helfern vorbei:"
                + Environment.NewLine + string.Join(Environment.NewLine, verstoesse));
        }

        [TestMethod]
        [DataRow("await server.StartAsync(new BuzzerServerOptions { Port = 5999 });")]
        [DataRow("var s = new BuzzerServer(); await s.StartAsync();")]
        [DataRow("server = new BuzzerServer { GetGame = () => game }; await server.StartAsync(ct: default);")]
        [DataRow("vm.BuzzerPort = 5999;")]
        [DataRow("await ((AsyncRelayCommand)vm.StartServerCommand).ExecuteAsync(null);")]
        [DataRow("vm.BuzzerBindAddress = \"0.0.0.0\";")]
        [DataRow("var l = new TcpListener(IPAddress.Any, 5999);")]
        [DataRow("var l = new TcpListener(IPAddress.IPv6Any, 5999);")]
        [DataRow("var l = new TcpListener(5999);")]
        [DataRow("var l = new TcpListener(IPAddress.Parse(\"192.168.1.2\"), 5999);")]
        [DataRow("var l = TcpListener.Create(5999);")]
        [DataRow("listener.Prefixes.Add(\"http://+:5999/\");")]
        [DataRow("listener.Prefixes.Add(\"http://*:5999/\");")]
        [DataRow("builder.WebHost.UseUrls(\"http://localhost:5999\");")]
        [DataRow("factory.UseKestrel();")]
        [DataRow("k.ListenAnyIP(5999);")]
        public void TheGuardCatchesEveryWayAroundLoopback(string zeile)
            => Assert.AreNotEqual(0, Verstoesse("Probe.cs", zeile, istHelfer: false).Count, $"Nicht erkannt: {zeile}");

        [TestMethod]
        [DataRow("await HubTest.StartAsync(server, TestPort);")]
        [DataRow("await StarteServerAsync(serverVm);")]
        [DataRow("var l = new TcpListener(IPAddress.Loopback, 0);")]
        [DataRow("var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);")]
        [DataRow("listener.Prefixes.Add($\"http://127.0.0.1:{port}/\");")]
        [DataRow("Assert.AreEqual(BuzzerServerOptions.AnyAddress, new BuzzerServerViewModel().BuzzerBindAddress);")]
        [DataRow("var verbindung = new HubConnectionBuilder().Build(); await verbindung.StartAsync();")]
        public void TheGuardLetsLoopbackPass(string zeile)
            => Assert.AreEqual(0, Verstoesse("Probe.cs", zeile, istHelfer: false).Count, string.Join(" | ", Verstoesse("Probe.cs", zeile, false)));
    }
}
