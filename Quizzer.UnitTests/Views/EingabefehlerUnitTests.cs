using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Validators;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Was ein Feld sagt, wenn die Eingabe nicht stimmt.
    /// <para>
    /// <b>Gemessen 2026-09-07: nichts.</b> Die Prüfregeln lieferten ihre Begründung
    /// („Numbers only", „Must be &gt;= 0") an niemanden — keine einzige Maske band auf
    /// <c>Validation.Errors</c>. Sichtbar war nur der rote Rahmen, den WPF selbst zeichnet. Der
    /// Wert kam still nicht am Modell an, und der Spielleiter stand vor einem roten Kasten ohne
    /// Grund. Englisch waren die Texte obendrein.
    /// </para>
    /// </summary>
    [TestClass]
    public class EingabefehlerUnitTests
    {
        /// <summary>
        /// <b>Der rote Rahmen bekommt einen Text.</b>
        /// <para>
        /// Gemessen wird am fertig ausgelegten Element, nicht an der Stilvorlage: ein Trigger, der
        /// zwar dasteht, aber von einem lokal gesetzten Wert überstimmt wird, wäre sonst grün.
        /// </para>
        /// </summary>
        [TestMethod]
        public void AFieldWithABadValueSaysWhatIsWrong()
        {
            object? hinweis = null;
            var hatFehler = false;

            UiTestHost.Run(() =>
            {
                var feld = new TextBox { DataContext = new Ziel() };

                var bindung = new Binding(nameof(Ziel.Zahl))
                {
                    UpdateSourceTrigger = UpdateSourceTrigger.Explicit,
                    ValidatesOnExceptions = true,
                    NotifyOnValidationError = true,
                };

                bindung.ValidationRules.Add(new PositiveIntValidationRule());

                feld.SetBinding(TextBox.TextProperty, bindung);

                var fenster = new Window { Content = feld };

                fenster.Show();

                feld.Text = "keine Zahl";

                feld.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();

                hatFehler = Validation.GetHasError(feld);
                hinweis = feld.ToolTip;

                fenster.Close();
            });

            Assert.IsTrue(hatFehler,
                "Die Pruefregel hat 'keine Zahl' durchgelassen - dann misst der Rest nichts.");

            Assert.IsNotNull(hinweis,
                "Das Feld ist rot, sagt aber nicht warum. Genau das war der Zustand bis "
                + "2026-09-07: der rote Rahmen ohne einen Satz dazu.");

            StringAssert.Contains(hinweis!.ToString() ?? string.Empty, "ganze Zahl",
                "Der Hinweis nennt den Grund nicht: " + hinweis);
        }

        /// <summary>
        /// <b>Die Gegenrichtung.</b> Ein gültiger Wert bekommt keinen Hinweis - sonst hinge an
        /// jedem Feld dauerhaft ein Fehlertext.
        /// </summary>
        [TestMethod]
        public void AGoodValueGetsNoHint()
        {
            object? hinweis = "vorbelegt";
            var hatFehler = true;

            UiTestHost.Run(() =>
            {
                var feld = new TextBox { DataContext = new Ziel() };

                var bindung = new Binding(nameof(Ziel.Zahl))
                {
                    UpdateSourceTrigger = UpdateSourceTrigger.Explicit,
                };

                bindung.ValidationRules.Add(new PositiveIntValidationRule());

                feld.SetBinding(TextBox.TextProperty, bindung);

                var fenster = new Window { Content = feld };

                fenster.Show();

                feld.Text = "7";

                feld.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();

                hatFehler = Validation.GetHasError(feld);
                hinweis = feld.ToolTip;

                fenster.Close();
            });

            Assert.IsFalse(hatFehler, "Eine gueltige 7 wurde als Fehler gewertet.");

            Assert.IsNull(hinweis, "An einem gueltigen Feld haengt ein Fehlertext: " + hinweis);
        }

        /// <summary>
        /// Die Begründungen sind deutsch. <b>Sichtbarer Text trägt echte Umlaute</b>
        /// (standards-allgemein §1) - bis 2026-09-07 waren es englische Brocken.
        /// </summary>
        [TestMethod]
        public void TheReasonsAreGerman()
        {
            var texte = new List<string>();

            void Sammle(ValidationRule regel, string eingabe)
            {
                var ergebnis = regel.Validate(eingabe, CultureInfo.CurrentCulture);

                if (!ergebnis.IsValid)
                    texte.Add(ergebnis.ErrorContent?.ToString() ?? string.Empty);
            }

            Sammle(new PositiveIntValidationRule(), string.Empty);
            Sammle(new PositiveIntValidationRule(), "abc");
            Sammle(new PositiveIntValidationRule(), "-3");
            Sammle(new PositiveDoubleValidationRule(), string.Empty);
            Sammle(new PositiveDoubleValidationRule(), "abc");
            Sammle(new PositiveDoubleValidationRule(), "-3");

            Assert.AreEqual(6, texte.Count,
                "Es wurden nicht alle sechs Faelle als ungueltig erkannt - dann prueft der Rest "
                + "die falsche Menge. Gesammelt: " + string.Join(" | ", texte));

            foreach (var text in texte)
            {
                Assert.IsFalse(
                    text.Contains("only", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("Required", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("Must be", StringComparison.OrdinalIgnoreCase),
                    "Eine Begruendung ist noch englisch: " + text);

                Assert.IsTrue(text.EndsWith('.'),
                    "Eine Begruendung ist kein Satz: " + text);
            }
        }

        /// <summary>Ein Ziel, an das gebunden werden kann.</summary>
        private sealed class Ziel
        {
            public int Zahl { get; set; }
        }
    }
}
