using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows;

namespace Quizzer.UnitTests
{
    /// <summary>
    /// Das Schließen des Anmeldefensters darf das Programm nicht beenden.
    /// <para>
    /// <b>Gemeldet 2026-09-06:</b> „sobald ich anmelden klicke ... beendet das programm".
    /// Ursache ist der Standard von WPF: <c>Application.ShutdownMode</c> ist
    /// <c>OnLastWindowClose</c>, und beim Start ist das Anmeldefenster das <b>einzige</b>
    /// Fenster. Sobald es sich schließt, sind null Fenster offen - WPF beendet die Anwendung,
    /// und das <c>Show()</c> des Hauptfensters kommt nie zum Zug.
    /// </para>
    /// <para>
    /// <b>Warum kein Test das je gefunden hat, und warum dieser hier eine Falle ist.</b>
    /// <c>UiTestHost</c> erzeugt seine <c>Application</c> mit
    /// <c>ShutdownMode = OnExplicitShutdown</c> - also genau mit der Einstellung, deren Fehlen
    /// der Defekt ist. <b>Der Prüfstand hat den Fehler zugedeckt.</b> Deshalb setzt dieser Test
    /// den Modus zuerst ausdrücklich auf den WPF-Standard zurück; ohne diesen Schritt wäre er
    /// grün, ohne irgendetwas zu messen.
    /// </para>
    /// <para>
    /// <b>Was hier nicht gemessen wird:</b> ob <c>App.OnStartup</c> die Methode auch wirklich
    /// aufruft. Das braucht eine gestartete <c>Application</c> mit echtem Anmeldefenster, und
    /// die gibt es im Testlauf nicht - ein Test, der ein WPF-Programm wirklich beendet, nimmt
    /// den ganzen Lauf mit. Der Aufruf steht in <c>App.OnStartup</c> als erste Zeile nach
    /// <c>base.OnStartup</c>.
    /// </para>
    /// </summary>
    [TestClass]
    public class StartupShutdownModeUnitTests
    {
        /// <summary>
        /// Nach dem Setzen beendet das Schließen des letzten Fensters die Anwendung nicht mehr.
        /// </summary>
        [TestMethod]
        public void TheLoginWindowClosingMustNotEndTheApplication()
        {
            ShutdownMode vorher = default;
            ShutdownMode nachher = default;
            Application? app = null;

            UiTestHost.Run(() =>
            {
                app = Application.Current;

                // Auf den WPF-Standard zurueck - sonst misst dieser Test die Einstellung des
                // Pruefstands statt die der Anwendung.
                app.ShutdownMode = ShutdownMode.OnLastWindowClose;
                vorher = app.ShutdownMode;

                App.FensterModusFuerAnmeldungSetzen();
                nachher = app.ShutdownMode;

                // Der Pruefstand braucht seine Einstellung zurueck, sonst nimmt das naechste
                // geschlossene Fenster den gesamten Testlauf mit.
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            });

            Assert.IsNotNull(app, "Es gab keine laufende Anwendung - der Test misst nichts.");

            Assert.AreEqual(ShutdownMode.OnLastWindowClose, vorher,
                "Der Ausgangszustand ist nicht der WPF-Standard. Dann prueft dieser Test nicht "
                + "den Fall, der am 2026-09-06 gemeldet wurde.");

            Assert.AreNotEqual(ShutdownMode.OnLastWindowClose, nachher,
                "Der Beendigungsmodus steht weiter auf OnLastWindowClose. Beim Start ist das "
                + "Anmeldefenster das einzige Fenster - sobald es sich schliesst, beendet WPF "
                + "das Programm, und das Hauptfenster kommt nie.");

            Assert.AreEqual(ShutdownMode.OnExplicitShutdown, nachher,
                "Erwartet ist OnExplicitShutdown: das Programm endet dann nur dort, wo es "
                + "ausdruecklich beendet wird - nach einer abgebrochenen Anmeldung oder beim "
                + "Schliessen des Hauptfensters.");
        }

        /// <summary>
        /// Und die Wirkung, nicht nur die Einstellung: mit <c>OnExplicitShutdown</c> überlebt die
        /// Anwendung, dass ihr letztes Fenster geschlossen wird.
        /// <para>
        /// <b>Sie bewacht nebenbei den Prüfstand selbst.</b> Sie verlangt, dass ihr Fenster das
        /// einzige ist. Erzeugt <c>UiTestHost</c> wieder ein <c>Quizzer.App</c> statt einer
        /// schlichten <c>Application</c>, läuft <c>App.OnStartup</c> mit und das Anmeldefenster
        /// steht offen - dann fällt diese Zusicherung. Gemessen: mit der alten Fassung meldet
        /// sie „Offen waren: LoginView, Window".
        /// </para>
        /// <para>
        /// <b>Die Gegenrichtung lässt sich nicht messen</b>, und das ist keine Nachlässigkeit:
        /// ein Testlauf, der die Anwendung unter <c>OnLastWindowClose</c> wirklich beendet,
        /// nimmt sich selbst mit. Der Beleg dafür ist die Meldung des Nutzers vom 2026-09-06 -
        /// gemessen hat es das Programm auf seinem Rechner.
        /// </para>
        /// </summary>
        [TestMethod]
        public void WithExplicitShutdownTheApplicationSurvivesItsLastWindow()
        {
            var lebtNoch = false;
            var fensterVorher = -1;
            var offene = new List<string>();
            var fensterNachher = -1;

            UiTestHost.Run(() =>
            {
                var app = Application.Current;

                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                var fenster = new Window { Width = 100, Height = 100, ShowInTaskbar = false };

                fenster.Show();
                fensterVorher = app.Windows.Count;

                foreach (Window w in app.Windows)
                    offene.Add(w.GetType().Name);

                fenster.Close();
                fensterNachher = app.Windows.Count;

                lebtNoch = !app.Dispatcher.HasShutdownStarted;
            });

            Assert.AreEqual(1, fensterVorher,
                "Es war nicht genau ein Fenster offen - dann misst der Test nicht den Fall des "
                + "Anmeldefensters. Offen waren: " + string.Join(", ", offene));

            Assert.AreEqual(0, fensterNachher,
                "Nach dem Schliessen war noch ein Fenster offen. Dann sagt der Test nichts "
                + "darueber aus, was beim LETZTEN Fenster geschieht.");

            Assert.IsTrue(lebtNoch,
                "Die Anwendung hat sich beendet, obwohl OnExplicitShutdown gesetzt war.");
        }
    }
}
