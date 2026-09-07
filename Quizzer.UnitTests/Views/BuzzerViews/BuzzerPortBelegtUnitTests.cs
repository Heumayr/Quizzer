using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.Views.BuzzerViews;
using System.Net;
using System.Net.Sockets;

namespace Quizzer.UnitTests.Views.BuzzerViews
{
    /// <summary>
    /// Was der Spielleiter liest, wenn der Buzzer-Server nicht starten kann.
    /// <para>
    /// Das ist der abendkritischste Fehlerfall des Buzzers: ohne Server buzzert niemand. Der
    /// häufigste Grund ist ein belegter Port — eine zweite Quizzer-Instanz, ein Testlauf, ein
    /// anderes Programm.
    /// </para>
    /// <para>
    /// <b>Die Portnummer stand bis 2026-09-07 als „5000" im Satz</b>, während
    /// <c>BuzzerPort</c> längst setzbar war. Eine abgeschriebene Zahl in Prosa altert still;
    /// diese Zusicherung wird rot.
    /// </para>
    /// </summary>
    [TestClass]
    public class BuzzerPortBelegtUnitTests
    {
        /// <summary>Ein Port, den in diesem Testlauf sonst niemand benutzt.</summary>
        private const int Port = 5407;

        private RecordingUserPrompt prompt = null!;

        [TestInitialize]
        public void SetUp()
        {
            prompt = new RecordingUserPrompt(answer: true);
            UserPrompt.Current = prompt;
        }

        [TestCleanup]
        public void TearDown() => UserPrompt.Reset();

        /// <summary>
        /// Ein belegter Port wird gemeldet - mit der Nummer, um die es wirklich geht.
        /// </summary>
        [TestMethod]
        public async Task ABlockedPortIsReportedWithItsNumber()
        {
            // IPAddress.Any, nicht Loopback: Kestrel bindet auf 0.0.0.0. Gemessen 2026-09-07 -
            // ein Blockierer auf 127.0.0.1 stoert dabei NICHT, der Server startete munter, und
            // die Zusicherung war aus dem falschen Grund rot.
            var blockierer = new TcpListener(IPAddress.Any, Port);

            blockierer.Start();

            var vm = new BuzzerServerViewModel { BuzzerPort = Port };

            try
            {
                await ((AsyncRelayCommand)vm.StartServerCommand).ExecuteAsync(null);

                Assert.AreEqual(1, prompt.Informs.Count,
                    "Der gescheiterte Start wurde nicht gemeldet - der Spielleiter steht vor "
                    + "einem Knopf, der nichts tut.");

                // NUR der eigene Satz, nicht die angehaengte Ausnahmemeldung. Die von Kestrel
                // enthaelt den Port ohnehin ("Failed to bind to address http://0.0.0.0:5407") -
                // gemessen 2026-09-07: gegen die ganze Meldung geprueft blieb diese Zusicherung
                // gruen, als die 5000 absichtlich wieder fest in den Satz geschrieben wurde. Sie
                // mass nichts.
                var satz = prompt.Informs[0].Message.Split(
                    Environment.NewLine + Environment.NewLine)[0];

                StringAssert.Contains(satz, Port.ToString(),
                    "Der Satz nennt den belegten Port nicht: " + satz);

                Assert.IsFalse(satz.Contains("5000", StringComparison.Ordinal),
                    "Im Satz steht eine abgeschriebene 5000 statt des wirklich benutzten Ports. "
                    + "Gelesen wurde: " + satz);

                Assert.IsFalse(vm.IsBuzzerServerRunning,
                    "Der Server gilt als laufend, obwohl er nicht starten konnte.");

                // Nichts darf im Fehlerfenster gelandet sein: die Meldung ist der ganze Zweck,
                // eine Stapelspur mitten im Spielaufbau waere das Gegenteil.
                TestEnvironment.ThrowIfAnythingWasSwallowed();
            }
            finally
            {
                // Falls er wider Erwarten doch startet, darf er den Port nicht fuer die
                // Gegenrichtung behalten - sonst faellt DIE, und der Befund zeigt nach hinten.
                await ((AsyncRelayCommand)vm.StopServerCommand).ExecuteAsync(null);

                blockierer.Stop();
            }
        }

        /// <summary>
        /// <b>Die Gegenrichtung.</b> Ist der Port frei, kommt keine Meldung und der Server läuft -
        /// sonst wäre die Zusicherung oben auch grün, wenn der Start <i>immer</i> scheiterte.
        /// </summary>
        [TestMethod]
        public async Task AFreePortStartsWithoutAWord()
        {
            var vm = new BuzzerServerViewModel { BuzzerPort = Port };

            try
            {
                await ((AsyncRelayCommand)vm.StartServerCommand).ExecuteAsync(null);

                Assert.AreEqual(0, prompt.Informs.Count,
                    "Es wurde gemeldet, obwohl der Port frei ist: "
                    + string.Join(" | ", prompt.Informs.Select(i => i.Message)));

                Assert.IsTrue(vm.IsBuzzerServerRunning, "Der Server laeuft nicht.");
            }
            finally
            {
                await ((AsyncRelayCommand)vm.StopServerCommand).ExecuteAsync(null);
            }
        }
    }
}
