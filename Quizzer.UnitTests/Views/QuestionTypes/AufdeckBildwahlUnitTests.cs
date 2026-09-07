using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Views.QuestionTypes;
using System.IO;

namespace Quizzer.UnitTests.Views.QuestionTypes
{
    /// <summary>
    /// Was der Bildwähler der Aufdeckfrage annimmt.
    /// <para>
    /// <b>Gemessen 2026-09-07:</b> der Dialog bot „Alle Dateien" an. Ein damit gewähltes
    /// <c>.mp4</c> wurde in den Datenordner kopiert und als Bild der Frage eingetragen -
    /// danach ließ sich die Frage <b>nicht mehr einrichten</b>, weil schon das Öffnen des
    /// Editors am Laden scheiterte. Am Quizabend warf dieselbe Stelle bei jedem Schrittwechsel.
    /// </para>
    /// <para>
    /// Der Wurf selbst ist seit dem Bildlader weg; hier geht es darum, dass eine Nicht-Bilddatei
    /// gar nicht erst an der Frage landet - <b>und zwar vor dem Kopieren</b>, sonst bliebe sie
    /// als Leiche im Ressourcenordner liegen.
    /// </para>
    /// </summary>
    [TestClass]
    public class AufdeckBildwahlUnitTests
    {
        private static string Datei(string endung)
            => Path.Combine(Path.GetTempPath(), "quizzer-probe" + endung);

        /// <summary>Bilder kommen durch - alle fünf angebotenen Endungen.</summary>
        [TestMethod]
        public void EveryOfferedImageKindIsAccepted()
        {
            foreach (var endung in new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" })
            {
                Assert.IsTrue(RevealEditorView.IstBild(Datei(endung)),
                    $"{endung} wird abgewiesen, obwohl der Dateidialog es anbietet - dann laesst "
                    + "sich kein Bild mehr waehlen.");
            }
        }

        /// <summary>
        /// <b>Die Gegenrichtung, und sie ist der Grund für die Prüfung.</b> Was kein Bild ist,
        /// kommt nicht durch - auch eine Endung, die das Programm gar nicht kennt.
        /// </summary>
        [TestMethod]
        public void NonImagesAreRefused()
        {
            foreach (var endung in new[] { ".mp4", ".mp3", ".pdf", ".txt", ".xyz", "" })
            {
                Assert.IsFalse(RevealEditorView.IstBild(Datei(endung)),
                    $"'{endung}' gilt als Bild - eine solche Datei macht die Aufdeckfrage "
                    + "unbrauchbar, und es gibt keinen Weg mehr, ein anderes Bild zu waehlen.");
            }
        }
    }
}
