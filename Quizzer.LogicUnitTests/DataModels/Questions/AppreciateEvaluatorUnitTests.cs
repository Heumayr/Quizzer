using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;
using System.Globalization;

namespace Quizzer.LogicUnitTests.DataModels.Questions
{
    /// <summary>
    /// Die automatische Gewinnerermittlung der Schaetzfrage: wer am naechsten dran liegt,
    /// gewinnt - ueber alle Wertarten und Einheiten hinweg.
    /// </summary>
    [TestClass]
    public class AppreciateEvaluatorUnitTests
    {
        private static readonly Guid Anna = Guid.NewGuid();
        private static readonly Guid Bert = Guid.NewGuid();
        private static readonly Guid Cleo = Guid.NewGuid();

        private static AppreciateQestion Question(
            AppreciateValueKind kind, AppreciateUnit unit, double expected = 0, DateTime? date = null)
            => new()
            {
                ValueKind = kind,
                Unit = unit,
                ExpectedValue = expected,
                ExpectedDate = date,
            };

        private static Dictionary<Guid, string?> Guesses(
            string? anna = null, string? bert = null, string? cleo = null)
        {
            var map = new Dictionary<Guid, string?>();
            if (anna != null) map[Anna] = anna;
            if (bert != null) map[Bert] = bert;
            if (cleo != null) map[Cleo] = cleo;
            return map;
        }

        // --- Zahlen ---

        [TestMethod]
        public void TheClosestGuessWins()
        {
            var question = Question(AppreciateValueKind.Number, AppreciateUnit.Stueck, 1000);

            var outcome = AppreciateEvaluator.Evaluate(question, Guesses("900", "1010", "1500"));

            CollectionAssert.AreEqual(new[] { Bert }, outcome.WinnerIds.ToArray());
        }

        [TestMethod]
        public void OvershootingIsJustAsGoodAsUndershooting()
        {
            var question = Question(AppreciateValueKind.Number, AppreciateUnit.Stueck, 100);

            var outcome = AppreciateEvaluator.Evaluate(question, Guesses("80", "110"));

            CollectionAssert.AreEqual(new[] { Bert }, outcome.WinnerIds.ToArray(),
                "Es zaehlt der Abstand, nicht die Richtung.");
        }

        [TestMethod]
        public void OnATie_EveryoneLevelWins()
        {
            var question = Question(AppreciateValueKind.Number, AppreciateUnit.Stueck, 100);

            var outcome = AppreciateEvaluator.Evaluate(question, Guesses("90", "110", "500"));

            CollectionAssert.AreEquivalent(new[] { Anna, Bert }, outcome.WinnerIds.ToArray());
        }

        [TestMethod]
        public void AnExactHitWins()
        {
            var question = Question(AppreciateValueKind.Number, AppreciateUnit.Stueck, 42);

            var outcome = AppreciateEvaluator.Evaluate(question, Guesses("42", "43"));

            CollectionAssert.AreEqual(new[] { Anna }, outcome.WinnerIds.ToArray());
            Assert.AreEqual(0d, outcome.Guesses.First(g => g.PlayerId == Anna).Distance);
        }

        [TestMethod]
        public void TheGuessesComeBackSortedByDistance()
        {
            var question = Question(AppreciateValueKind.Number, AppreciateUnit.Stueck, 100);

            var outcome = AppreciateEvaluator.Evaluate(question, Guesses("500", "101", "150"));

            CollectionAssert.AreEqual(
                new[] { Bert, Cleo, Anna },
                outcome.Guesses.Select(g => g.PlayerId).ToArray());
        }

        // --- Unlesbares ---

        [TestMethod]
        public void AnUnreadableGuessDoesNotWin()
        {
            var question = Question(AppreciateValueKind.Number, AppreciateUnit.Stueck, 100);

            var outcome = AppreciateEvaluator.Evaluate(question, Guesses("keine Ahnung", "5000"));

            CollectionAssert.AreEqual(new[] { Bert }, outcome.WinnerIds.ToArray(),
                "Unlesbar darf nicht als Abstand null durchgehen.");
            Assert.IsFalse(outcome.Guesses.First(g => g.PlayerId == Anna).IsValid);
        }

        [TestMethod]
        public void AnUnreadableGuessSortsLast()
        {
            var question = Question(AppreciateValueKind.Number, AppreciateUnit.Stueck, 100);

            var outcome = AppreciateEvaluator.Evaluate(question, Guesses("Unsinn", "999"));

            Assert.AreEqual(Bert, outcome.Guesses.First().PlayerId);
            Assert.AreEqual(Anna, outcome.Guesses.Last().PlayerId);
        }

        [TestMethod]
        public void WhenNobodyGuessesReadably_ThereIsNoWinner()
        {
            var question = Question(AppreciateValueKind.Number, AppreciateUnit.Stueck, 100);

            var outcome = AppreciateEvaluator.Evaluate(question, Guesses("abc", ""));

            Assert.IsFalse(outcome.HasWinner);
        }

        [TestMethod]
        public void WithoutAnyGuesses_ThereIsNoWinner()
        {
            var question = Question(AppreciateValueKind.Number, AppreciateUnit.Stueck, 100);

            var outcome = AppreciateEvaluator.Evaluate(question, new Dictionary<Guid, string?>());

            Assert.IsFalse(outcome.HasWinner);
            Assert.AreEqual(0, outcome.Guesses.Count);
        }

        // --- Zahlenformate ---

        [TestMethod]
        [DataRow("3,5", 3.5)]
        [DataRow("3.5", 3.5)]
        [DataRow("  7  ", 7.0)]
        [DataRow("-2,5", -2.5)]
        public void CommaAndPointBothCount(string input, double expected)
        {
            var value = AppreciateEvaluator.ParseGuess(
                input, AppreciateValueKind.Number, AppreciateUnit.Stueck);

            Assert.AreEqual(expected, value!.Value, 0.0001);
        }

        // --- Einheiten ---

        [TestMethod]
        public void LengthsAreComparedInMetres()
        {
            // Sollwert 2 km; Anna tippt 1900 m gedacht als 1,9 km, Bert 2,5 km.
            var question = Question(AppreciateValueKind.Length, AppreciateUnit.Kilometer, 2);

            var outcome = AppreciateEvaluator.Evaluate(question, Guesses("1,9", "2,5"));

            CollectionAssert.AreEqual(new[] { Anna }, outcome.WinnerIds.ToArray());
            Assert.AreEqual(2000d, AppreciateEvaluator.ExpectedInBaseUnit(question)!.Value, 0.001);
        }

        [TestMethod]
        [DataRow(AppreciateUnit.Kilometer, 1.0, 1000.0)]
        [DataRow(AppreciateUnit.Meter, 1.0, 1.0)]
        [DataRow(AppreciateUnit.Zentimeter, 100.0, 1.0)]
        [DataRow(AppreciateUnit.Tonne, 1.0, 1_000_000.0)]
        [DataRow(AppreciateUnit.Kilogramm, 1.0, 1000.0)]
        [DataRow(AppreciateUnit.Milliliter, 1000.0, 1.0)]
        [DataRow(AppreciateUnit.Minuten, 1.0, 60.0)]
        [DataRow(AppreciateUnit.Stunden, 1.0, 3600.0)]
        public void UnitsConvertToTheirBase(AppreciateUnit unit, double value, double expected)
        {
            Assert.AreEqual(expected, AppreciateUnits.ToBase(value, unit), 0.0001);
        }

        [TestMethod]
        public void EveryUnitBelongsToExactlyOneKind()
        {
            foreach (var unit in Enum.GetValues<AppreciateUnit>())
            {
                var matches = AppreciateUnits.All.Count(u => u.Unit == unit);
                Assert.AreEqual(1, matches, $"{unit} ist nicht genau einmal eingetragen.");
            }
        }

        [TestMethod]
        public void EveryKindOffersAtLeastOneUnit()
        {
            foreach (var kind in Enum.GetValues<AppreciateValueKind>())
            {
                Assert.IsTrue(AppreciateUnits.For(kind).Count > 0, $"{kind} hat keine Einheit.");
            }
        }

        // --- Datum ---

        [TestMethod]
        public void TheClosestDateWins()
        {
            var question = Question(AppreciateValueKind.Date, AppreciateUnit.Datum,
                date: new DateTime(1969, 7, 20));

            var outcome = AppreciateEvaluator.Evaluate(
                question, Guesses("1969-07-25", "1969-08-20"));

            CollectionAssert.AreEqual(new[] { Anna }, outcome.WinnerIds.ToArray());
        }

        [TestMethod]
        public void DateDistanceIsCountedInDays()
        {
            var question = Question(AppreciateValueKind.Date, AppreciateUnit.Datum,
                date: new DateTime(2000, 1, 1));

            var outcome = AppreciateEvaluator.Evaluate(question, Guesses("2000-01-11"));

            Assert.AreEqual(10d, outcome.Guesses.Single().Distance!.Value, 0.001);
        }

        [TestMethod]
        public void APlainYearCountsAsTheFirstOfJanuary()
        {
            var value = AppreciateEvaluator.ParseGuess(
                "1969", AppreciateValueKind.Date, AppreciateUnit.Datum);

            Assert.AreEqual(new DateTime(1969, 1, 1).ToOADate(), value!.Value, 0.001);
        }

        [TestMethod]
        public void AnUnreadableDateIsOutOfTheRunning()
        {
            var question = Question(AppreciateValueKind.Date, AppreciateUnit.Datum,
                date: new DateTime(2000, 1, 1));

            var outcome = AppreciateEvaluator.Evaluate(question, Guesses("irgendwann", "2000-01-05"));

            CollectionAssert.AreEqual(new[] { Bert }, outcome.WinnerIds.ToArray());
        }

        // --- Anzeige ---

        [TestMethod]
        public void TheExpectedValueIsShownWithItsUnit()
        {
            var question = Question(AppreciateValueKind.Length, AppreciateUnit.Kilometer, 3.798);

            StringAssert.Contains(AppreciateEvaluator.DescribeExpected(question), "km");
        }

        [TestMethod]
        public void APlainNumberIsShownWithoutAUnit()
        {
            var question = Question(AppreciateValueKind.Number, AppreciateUnit.Stueck, 500);

            Assert.AreEqual("500", AppreciateEvaluator.DescribeExpected(question).Trim());
        }

        [TestMethod]
        [DataRow(AppreciateValueKind.Date, "date")]
        [DataRow(AppreciateValueKind.Number, "number")]
        [DataRow(AppreciateValueKind.Length, "number")]
        public void TheBrowserGetsTheRightInputType(AppreciateValueKind kind, string expected)
        {
            Assert.AreEqual(expected, AppreciateEvaluator.InputTypeFor(kind));
        }

        [TestMethod]
        public void ThePlaceholderNamesTheUnit()
        {
            var placeholder = AppreciateEvaluator.PlaceholderFor(
                AppreciateValueKind.Mass, AppreciateUnit.Kilogramm);

            StringAssert.Contains(placeholder, "kg");
        }

        [TestMethod]
        public void Evaluate_RejectsNull()
        {
            Assert.ThrowsExactly<ArgumentNullException>(
                () => AppreciateEvaluator.Evaluate(null!, new Dictionary<Guid, string?>()));
        }
    }
}
