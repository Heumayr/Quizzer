using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Teams;
using Quizzer.Logic.Spielablauf;
using System.Reflection;

namespace Quizzer.LogicUnitTests.Logic.Spielablauf
{
    /// <summary>
    /// P8-5 (Durchsicht M37, P10): zwei Stellen fragen „gebucht?“ - <see cref="Teamwertung.OhneBuchung"/> an der gespeicherten
    /// Zeile (Stempel, Behalten, „Wer spielt mit?“) und <see cref="LetzteWertung.HatGebucht"/> seit dem Öffnen (Sperren gegen
    /// eine zweite Buchung). Über jedes Wertungsfeld von <see cref="QuestionResult"/> urteilen sie gleich - auch über eines, das
    /// später dazukommt. Einzig „richtig beantwortet“ trennt sie, mit Absicht: „richtig“ setzt es schon als Vorschlag (darum
    /// zählt es nicht als Buchung), gespeichert aber entscheidet es, wer auswählt und wer die Zelle gewann (darum ist die Zeile
    /// nicht leer).
    /// </summary>
    [TestClass]
    public class BuchungsmassUnitTests
    {
        /// <summary>Kein Wertungsfeld: Schlüssel, Stempel - und der Zeitpunkt, der nur mit dem Bestwert gesetzt wird.</summary>
        private static readonly HashSet<string> KeinWertungsfeld =
        [
            nameof(QuestionResult.PlayerId), nameof(QuestionResult.QuestionBaseId), nameof(QuestionResult.GameId),
            nameof(QuestionResult.GameGridCoordinateId), nameof(QuestionResult.GameTeamId), nameof(QuestionResult.RecordedAt),
        ];

        private static IEnumerable<PropertyInfo> Wertungsfelder()
            => typeof(QuestionResult).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.CanWrite && (p.PropertyType.IsValueType || Nullable.GetUnderlyingType(p.PropertyType) != null))
                .Where(p => !KeinWertungsfeld.Contains(p.Name));

        private static (bool Leer, bool Gebucht) Urteile(PropertyInfo feld)
        {
            var spieler = Guid.NewGuid();
            var zelle = new GameGridCoordinate { Id = Guid.NewGuid() };
            var vorher = new QuestionResult { Id = Guid.NewGuid(), PlayerId = spieler };
            var merk = LetzteWertung.Merke(new Game { Id = Guid.NewGuid() }, zelle, [vorher]);

            var nachher = vorher.CloneWithoutReferences();
            feld.SetValue(nachher, feld.PropertyType == typeof(bool) ? true : Convert.ChangeType(1, Nullable.GetUnderlyingType(feld.PropertyType) ?? feld.PropertyType));

            return (Teamwertung.OhneBuchung(nachher), merk.HatGebucht([nachher]));
        }

        [TestMethod]
        public void BothMeasuresJudgeEveryScoringFieldAlike()
        {
            var felder = Wertungsfelder().ToList();
            Assert.IsTrue(felder.Count >= 9, "Die Wertungsfelder wurden nicht gefunden: " + string.Join(", ", felder.Select(f => f.Name)));

            var abweichend = felder
                .Where(f => f.Name != nameof(QuestionResult.CorrectAnswered))
                .Select(f => (f.Name, Urteil: Urteile(f)))
                .Where(f => f.Urteil != (false, true))
                .Select(f => $"{f.Name}: leer={f.Urteil.Leer}, gebucht={f.Urteil.Gebucht}")
                .ToList();

            Assert.AreEqual(0, abweichend.Count, "Nicht beide Maße zählen das Feld als Buchung: " + string.Join("; ", abweichend));
        }

        [TestMethod]
        public void CorrectAnsweredAloneIsAResultButNoBooking()
        {
            var feld = typeof(QuestionResult).GetProperty(nameof(QuestionResult.CorrectAnswered))!;

            Assert.AreEqual((false, false), Urteile(feld),
                "„Richtig beantwortet“ allein: die gespeicherte Zeile ist nicht leer, gebucht ist aber nichts.");
        }
    }
}
