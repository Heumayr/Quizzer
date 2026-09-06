using System.Windows;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Erzeugt ein Fenster und schließt es wieder - auch wenn dazwischen etwas schiefgeht.
    /// <para>
    /// <b>Warum es das braucht.</b> WPF trägt jedes erzeugte <see cref="Window"/> in
    /// <c>Application.Windows</c> ein, und der Oberflächen-Thread aus <c>UiTestHost</c> lebt bis
    /// zum Prozessende. Ein Test, der zwanzig Fenster baut und keines schließt, hinterlässt sie
    /// also dem gesamten restlichen Testlauf.
    /// </para>
    /// <para>
    /// <b>Gemessen am 2026-09-06:</b> ein <c>LoginView</c> stand während des ganzen Laufs offen.
    /// Aufgefallen ist es erst, als eine neue Zusicherung wissen musste, ob ihr Fenster das
    /// <b>letzte</b> ist - sie konnte diesen Zustand gar nicht mehr herstellen. Ein liegen
    /// gebliebenes Fenster macht keinen Test rot; es nimmt späteren nur die Messbarkeit.
    /// </para>
    /// </summary>
    internal sealed class FensterAufraeumer : IDisposable
    {
        public FensterAufraeumer(Type fenstertyp)
        {
            Fenster = (Window)Activator.CreateInstance(fenstertyp)!;
        }

        /// <summary>Das erzeugte Fenster.</summary>
        public Window Fenster { get; }

        public void Dispose()
        {
            try
            {
                Fenster.Close();
            }
            catch (InvalidOperationException)
            {
                // Ein Fenster, das nie gezeigt wurde, laesst sich in manchen Zustaenden nicht
                // schliessen. Das ist kein Fehler des Tests - und ein Wurf hier wuerde den
                // eigentlichen Befund verdecken.
            }
        }
    }
}
