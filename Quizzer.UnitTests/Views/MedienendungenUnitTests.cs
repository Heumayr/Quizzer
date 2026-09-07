using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.Views.QuestionTypes;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Der Dateidialog für Medien und die Annahmeliste müssen dasselbe sagen.
    /// <para>
    /// <b>Beide Richtungen tun weh.</b> Eine Endung, die der Filter anbietet und
    /// <c>DetectResourceType</c> nicht kennt, wirft beim Übernehmen — der Nutzer bekommt ein
    /// Fehlerfenster für eine Datei, zu der ihn das Programm selbst geführt hat. Umgekehrt ist
    /// eine angenommene Endung, die im Filter fehlt, für ihn schlicht unerreichbar.
    /// </para>
    /// <para>
    /// <b>Und der Typ muss stimmen.</b> Steht <c>.m4a</c> unter „Video", landet eine Tonspur als
    /// Video im Schritt — der Spielleiter sieht dann eine schwarze Fläche statt des
    /// Ton-Platzhalters.
    /// </para>
    /// </summary>
    [TestClass]
    public class MedienendungenUnitTests
    {
        /// <summary>Zerlegt den Dialogfilter in „Gruppenname → Endungen".</summary>
        private static IEnumerable<(string Gruppe, string Endung)> AusDemFilter()
        {
            var teile = EditQuestionViewModel.Medienfilter.Split('|');

            // Der Filter ist paarweise aufgebaut: Name, Muster, Name, Muster …
            for (var i = 0; i + 1 < teile.Length; i += 2)
            {
                foreach (var muster in teile[i + 1].Split(';', StringSplitOptions.RemoveEmptyEntries))
                    yield return (teile[i], muster.TrimStart('*').ToLowerInvariant());
            }
        }

        private static ResourceType ErwarteterTyp(string gruppe) => gruppe switch
        {
            "Bilder" => ResourceType.Image,
            "Ton" => ResourceType.Audio,
            "Video" => ResourceType.Video,
            "Dokumente" => ResourceType.Document,
            _ => throw new AssertFailedException(
                $"Unbekannte Gruppe im Filter: '{gruppe}'. Diese Zusicherung muss mitgezogen werden."),
        };

        /// <summary>Jede angebotene Endung wird auch angenommen — und als der richtige Typ.</summary>
        [TestMethod]
        public void EveryOfferedExtensionIsAcceptedAsTheRightType()
        {
            var geprueft = 0;

            foreach (var (gruppe, endung) in AusDemFilter())
            {
                var typ = FileHelper.DetectResourceType("probe" + endung);

                Assert.AreEqual(ErwarteterTyp(gruppe), typ,
                    $"Der Dialog bietet '{endung}' unter '{gruppe}' an, DetectResourceType "
                    + $"macht daraus aber {typ}.");

                geprueft++;
            }

            // Ohne diese Schranke waere die Schleife auch gruen, wenn der Filter leer waere oder
            // das Zerlegen danebengriffe - dieselbe Falle wie bei jeder Zaehlung ueber ein
            // Suchmuster.
            Assert.IsTrue(geprueft >= 20,
                $"Aus dem Filter kamen nur {geprueft} Endungen heraus - dann prueft diese "
                + "Zusicherung das Zerlegen und nicht den Filter.");
        }

        /// <summary>
        /// <b>Die Gegenrichtung.</b> Was angenommen wird, steht auch im Dialog. Ohne sie könnte
        /// eine Endung stillschweigend aus dem Filter fallen und wäre für den Spielleiter nicht
        /// mehr erreichbar — angenommen würde sie weiterhin.
        /// </summary>
        [TestMethod]
        public void EveryAcceptedExtensionIsOffered()
        {
            // Der Bestand der Annahmeliste, von Hand nachgefuehrt. Eine Liste aus dem Code zu
            // ziehen ginge nur ueber Reflexion in einen switch-Ausdruck hinein - das misst dann
            // den Uebersetzer, nicht die Absicht.
            var angenommen = new[]
            {
                ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp",
                ".mp4", ".avi", ".mov", ".wmv", ".mkv", ".webm", ".m4v",
                ".mp3", ".wav", ".ogg", ".flac", ".m4a", ".aac",
                ".pdf", ".doc", ".docx", ".txt",
            };

            var imFilter = AusDemFilter().Select(x => x.Endung).ToHashSet(StringComparer.Ordinal);

            foreach (var endung in angenommen)
            {
                // Erst nachweisen, dass sie wirklich angenommen wird - sonst prueft die Liste
                // sich selbst.
                _ = FileHelper.DetectResourceType("probe" + endung);

                Assert.IsTrue(imFilter.Contains(endung),
                    $"'{endung}' wird angenommen, steht aber in keinem Dialogfilter - der "
                    + "Spielleiter kann eine solche Datei gar nicht auswaehlen.");
            }
        }
    }
}
