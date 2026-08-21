using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;
using System.Globalization;

namespace Quizzer.LogicUnitTests.DataModels.Questions
{
    /// <summary>
    /// Die Datums-Schaetzfrage: die Spieler tippen ein Datum, wer am naechsten dran liegt,
    /// gewinnt. Der Abstand wird in Tagen gerechnet.
    /// </summary>
    [TestClass]
    public class AppreciateDateUnitTests
    {
        private static readonly Guid Anna = Guid.NewGuid();
        private static readonly Guid Bert = Guid.NewGuid();
        private static readonly Guid Cleo = Guid.NewGuid();

        private static AppreciateQestion QuestionFor(DateTime expected)
            => new()
            {
                ValueKind = AppreciateValueKind.Date,
                Unit = AppreciateUnit.Datum,
                ExpectedDate = expected,
            };

        private static AppreciateOutcome Evaluate(DateTime expected, params (Guid Player, string Guess)[] guesses)
            => AppreciateEvaluator.Evaluate(QuestionFor(expected),
                guesses.ToDictionary(g => g.Player, g => (string?)g.Guess));

        private static DateTime? Parse(string text)
        {
            var value = AppreciateEvaluator.ParseGuess(text, AppreciateValueKind.Date, AppreciateUnit.Datum);
            return value.HasValue ? DateTime.FromOADate(value.Value) : null;
        }

        // ── Schreibweisen ─────────────────────────────────────────────────

        [TestMethod]
        [DataRow("1.2.1974")]
        [DataRow("01.02.1974")]
        [DataRow("1974-02-01")]
        [DataRow("1. 2. 1974")]
        public void TheUsualSpellingsAllMeanTheFirstOfFebruary(string input)
        {
            Assert.AreEqual(new DateTime(1974, 2, 1), Parse(input));
        }

        /// <summary>
        /// Gemessen am 21.08.2026: unter en-US las <c>DateTime.TryParse</c> "1.2.1974" still als
        /// 2. Jaenner statt als 1. Februar - einen Monat daneben, ohne Fehlermeldung. Seither
        /// stehen die Tag-Monat-Jahr-Formate ausdruecklich in der Liste, damit die
        /// Windows-Sprache nichts mehr daran aendert.
        /// </summary>
        [TestMethod]
        [DataRow("de-AT")]
        [DataRow("en-US")]
        [DataRow("en-GB")]
        [DataRow("fr-FR")]
        public void TheSpellingIsReadTheSameUnderAnyWindowsLanguage(string culture)
        {
            var previous = CultureInfo.CurrentCulture;

            try
            {
                CultureInfo.CurrentCulture = new CultureInfo(culture);

                Assert.AreEqual(new DateTime(1974, 2, 1), Parse("1.2.1974"),
                    $"Unter {culture} wurde ein anderes Datum gelesen.");
                Assert.AreEqual(new DateTime(1974, 2, 1), Parse("1974-02-01"));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [TestMethod]
        public void APlainYearMeansTheFirstOfJanuary()
        {
            Assert.AreEqual(new DateTime(1974, 1, 1), Parse("1974"));
        }

        [TestMethod]
        [DataRow("irgendwann")]
        [DataRow("")]
        [DataRow("32.13.1974")]
        public void NonsenseIsNotADate(string input)
        {
            Assert.IsNull(Parse(input));
        }

        // ── Wer gewinnt ───────────────────────────────────────────────────

        [TestMethod]
        public void TheClosestDateWins()
        {
            var outcome = Evaluate(new DateTime(1974, 2, 1),
                (Anna, "1.1.1974"),      // 31 Tage davor
                (Bert, "10.2.1974"),     //  9 Tage danach
                (Cleo, "1.2.1975"));     // ein Jahr daneben

            CollectionAssert.AreEqual(new[] { Bert }, outcome.WinnerIds.ToArray());
        }

        [TestMethod]
        public void EarlierAndLaterCountTheSame()
        {
            var outcome = Evaluate(new DateTime(1974, 2, 1),
                (Anna, "22.1.1974"),     // 10 Tage davor
                (Bert, "11.2.1974"));    // 10 Tage danach

            CollectionAssert.AreEquivalent(new[] { Anna, Bert }, outcome.WinnerIds.ToArray(),
                "Es zaehlt der Abstand, nicht die Richtung - beide gewinnen.");
        }

        [TestMethod]
        public void AnExactDateWins()
        {
            var outcome = Evaluate(new DateTime(1974, 2, 1),
                (Anna, "1.2.1974"),
                (Bert, "2.2.1974"));

            CollectionAssert.AreEqual(new[] { Anna }, outcome.WinnerIds.ToArray());
            Assert.AreEqual(0d, outcome.Guesses.First(g => g.PlayerId == Anna).Distance);
        }

        [TestMethod]
        public void TheGuessesAreSortedByDistance()
        {
            var outcome = Evaluate(new DateTime(1974, 2, 1),
                (Anna, "1.2.1980"),
                (Bert, "3.2.1974"),
                (Cleo, "1.6.1974"));

            CollectionAssert.AreEqual(new[] { Bert, Cleo, Anna },
                outcome.Guesses.Select(g => g.PlayerId).ToArray());
        }

        [TestMethod]
        public void AnUnreadableDateIsOutOfTheRunning()
        {
            var outcome = Evaluate(new DateTime(1974, 2, 1),
                (Anna, "weiss nicht"),
                (Bert, "1.1.1990"));

            CollectionAssert.AreEqual(new[] { Bert }, outcome.WinnerIds.ToArray(),
                "Auch ein weit danebenliegendes Datum schlaegt eine unlesbare Eingabe.");
        }

        [TestMethod]
        public void AMixOfSpellingsIsFairlyCompared()
        {
            var outcome = Evaluate(new DateTime(1974, 2, 1),
                (Anna, "1974-02-05"),    // ISO, 4 Tage daneben
                (Bert, "10.2.1974"),     // Ortsformat, 9 Tage daneben
                (Cleo, "1974"));         // blosses Jahr, 31 Tage daneben

            CollectionAssert.AreEqual(new[] { Anna }, outcome.WinnerIds.ToArray(),
                "Wie jemand tippt, darf die Wertung nicht beeinflussen.");
        }

        // ── Was der Spielleiter sieht ─────────────────────────────────────

        [TestMethod]
        public void TheDistanceIsGivenInDays()
        {
            var question = QuestionFor(new DateTime(1974, 2, 1));
            var outcome = AppreciateEvaluator.Evaluate(question,
                new Dictionary<Guid, string?> { [Anna] = "10.2.1974" });

            Assert.AreEqual("9 Tage daneben",
                AppreciateEvaluator.DescribeDistance(question, outcome.Guesses.Single()));
        }

        [TestMethod]
        public void TheExpectedDateIsShownAsADate()
        {
            var question = QuestionFor(new DateTime(1974, 2, 1));

            var text = AppreciateEvaluator.DescribeExpected(question);

            StringAssert.Contains(text, "1974");
            Assert.IsFalse(text.Contains("Datum"),
                "Beim Sollwert steht das Datum selbst, nicht das Wort Datum.");
        }

        [TestMethod]
        public void ThePlayersGetADateFieldOnTheirPhone()
        {
            Assert.AreEqual("date", AppreciateEvaluator.InputTypeFor(AppreciateValueKind.Date));
            Assert.AreEqual("Datum",
                AppreciateEvaluator.PlaceholderFor(AppreciateValueKind.Date, AppreciateUnit.Datum));
        }
    }
}
