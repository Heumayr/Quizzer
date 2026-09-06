using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Quizzer.UnitTests
{
    /// <summary>
    /// Die Wache, die tote Bindungen meldet - kann sie überhaupt melden?
    /// <para>
    /// <b>Vier Zusicherungen hängen daran</b> (<c>AllWindowsBuildUnitTests</c>,
    /// <c>GameWindowsRenderUnitTests</c> zweimal, <c>PlayerWindowRenderUnitTests</c>), und jede
    /// prüft, dass die Liste der Wache <b>leer</b> ist. Eine Wache, die nie feuert, macht sie
    /// alle vier grün - und niemand merkt es.
    /// </para>
    /// <para>
    /// <b>Belegt war ihre Wirksamkeit bis 2026-09-07 nur durch einen Kommentar</b> („gemessen am
    /// 2026-09-06"), also durch eine einmalige Handmessung. Sie hängt an zwei fest verdrahteten
    /// englischen Textschnipseln aus der WPF-Meldung - ein Wechsel der .NET-Version genügt, und
    /// die vier Zusicherungen prüfen nichts mehr, ohne rot zu werden.
    /// </para>
    /// </summary>
    [TestClass]
    public class BindingErrorWatchUnitTests
    {
        /// <summary>Legt ein Element mit der übergebenen Bindung aus und liefert die Funde.</summary>
        private static int FundeBeimAuslegen(Binding bindung)
        {
            var anzahl = 0;

            UiTestHost.Run(() =>
            {
                using var wache = new BindingErrorWatch();

                var block = new TextBlock { DataContext = new { Vorhanden = "da" } };

                block.SetBinding(TextBlock.TextProperty, bindung);

                block.Measure(new Size(200, 50));
                block.Arrange(new Rect(0, 0, 200, 50));
                block.UpdateLayout();

                anzahl = wache.Errors.Count;
            });

            return anzahl;
        }

        /// <summary>Eine Bindung ins Leere wird gemeldet.</summary>
        [TestMethod]
        public void ADeadBindingIsReported()
        {
            var funde = FundeBeimAuslegen(new Binding("GibtEsNichtUndDarfNieGefundenWerden"));

            Assert.AreNotEqual(0, funde,
                "Die Wache meldet eine Bindung ins Leere nicht. Damit sind alle vier "
                + "Zusicherungen, die eine leere Fundliste erwarten, wertlos - sie waeren gruen, "
                + "ohne etwas zu pruefen. Naechstliegende Ursache: WPF formuliert die Meldung "
                + "anders als die beiden fest verdrahteten Schnipsel in BindingErrorWatch "
                + "(\"path error\", \"cannot find governing\").");
        }

        /// <summary>
        /// <b>Die Gegenrichtung.</b> Eine gesunde Bindung wird nicht gemeldet.
        /// <para>
        /// <b>Gemessen und eingeschränkt:</b> diese Zusicherung allein reicht <i>nicht</i>. Wird
        /// der Filter absichtlich auf „alles" gestellt, bleibt sie grün - denn eine gesunde
        /// Bindung erzeugt auf der Stufe <c>Warning</c> überhaupt keine Ausgabe, es gibt also
        /// nichts zu filtern. Deshalb steht daneben
        /// <see cref="OtherBindingNoiseIsNotCounted"/>, die den Filter wirklich fordert.
        /// </para>
        /// </summary>
        [TestMethod]
        public void AWorkingBindingIsNotReported()
        {
            var funde = FundeBeimAuslegen(new Binding("Vorhanden"));

            Assert.AreEqual(0, funde,
                "Die Wache meldet eine voellig gesunde Bindung - dann ist sie nicht zu "
                + "gebrauchen.");
        }

        /// <summary>
        /// <b>Die Gegenrichtung, die den Filter wirklich fordert.</b>
        /// <para>
        /// Der Kommentar in <c>BindingErrorWatch</c> sagt zu, dass anderes als eine fehlende
        /// Eigenschaft <i>Rauschen</i> ist und nicht mitgezählt wird. Ein Wert, der sich nicht
        /// in den Zieltyp wandeln lässt, erzeugt genau solches Rauschen: WPF meldet es, es ist
        /// aber keine Bindung ins Leere.
        /// </para>
        /// <para>
        /// <b>Warum es diese Zusicherung braucht:</b> nachgemessen blieben die beiden darüber
        /// grün, als der Filter absichtlich auf „alles" gestellt wurde - eine gesunde Bindung
        /// erzeugt auf der Stufe <c>Warning</c> gar keine Ausgabe, es gibt also nichts zu
        /// filtern. Erst hier wird der Filter gefordert.
        /// </para>
        /// <para>
        /// <b>Erst gemessen, dann geschrieben:</b> der erste Anlauf nahm einen werfenden
        /// Konverter. Der erzeugt kein Rauschen, sondern eine Ausnahme, die durch den Testlauf
        /// nach oben schlägt.
        /// </para>
        /// </summary>
        [TestMethod]
        public void OtherBindingNoiseIsNotCounted()
        {
            var funde = 0;

            UiTestHost.Run(() =>
            {
                using var wache = new BindingErrorWatch();

                // FontSize ist double, gebunden wird eine Zeichenkette, die keine Zahl ist.
                var block = new TextBlock { DataContext = new { KeineZahl = "abc" } };

                block.SetBinding(TextBlock.FontSizeProperty, new Binding("KeineZahl"));

                block.Measure(new Size(200, 50));
                block.Arrange(new Rect(0, 0, 200, 50));
                block.UpdateLayout();

                funde = wache.Errors.Count;
            });

            Assert.AreEqual(0, funde,
                "Ein nicht wandelbarer Wert wird als tote Bindung gezaehlt - dann meldet die "
                + "Wache Rauschen, und die vier Zusicherungen darauf werden rot, ohne dass eine "
                + "Bindung ins Leere zeigt. Gefunden: " + funde);
        }
    }
}
