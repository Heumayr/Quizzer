using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;

namespace Quizzer.LogicUnitTests.DataModels.Questions
{
    /// <summary>
    /// Die Einheitentabelle der Schaetzfrage: Vollstaendigkeit, Umrechnung und die
    /// Sonderfaelle, bei denen ein blosser Faktor nicht reicht.
    /// </summary>
    [TestClass]
    public class AppreciateUnitsUnitTests
    {
        // ── Vollstaendigkeit ──────────────────────────────────────────────

        [TestMethod]
        public void EveryUnitAppearsExactlyOnce()
        {
            foreach (var unit in Enum.GetValues<AppreciateUnit>())
            {
                Assert.AreEqual(1, AppreciateUnits.All.Count(u => u.Unit == unit),
                    $"{unit} ist nicht genau einmal eingetragen.");
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

        [TestMethod]
        public void EveryUnitCarriesASymbolExceptThePlainCount()
        {
            foreach (var info in AppreciateUnits.All.Where(u => u.Unit != AppreciateUnit.Stueck))
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(info.Symbol),
                    $"{info.Unit} hat kein Einheitenzeichen.");
            }

            Assert.AreEqual(string.Empty, AppreciateUnits.Info(AppreciateUnit.Stueck).Symbol,
                "Eine blosse Anzahl traegt bewusst kein Zeichen.");
        }

        [TestMethod]
        public void NoFactorIsZero()
        {
            foreach (var info in AppreciateUnits.All)
            {
                Assert.AreNotEqual(0d, info.FactorToBase,
                    $"{info.Unit} haette einen Faktor 0 - damit waere jede Umrechnung kaputt.");
            }
        }

        /// <summary>
        /// Je Groesse muss genau eine Einheit den Faktor 1 haben - das ist ihre Basiseinheit.
        /// Fehlt sie, ist die Tabelle in sich nicht stimmig.
        /// </summary>
        [TestMethod]
        public void EveryKindHasExactlyOneBaseUnit()
        {
            foreach (var kind in Enum.GetValues<AppreciateValueKind>())
            {
                var basisCount = AppreciateUnits.For(kind)
                    .Count(u => Math.Abs(u.FactorToBase - 1) < 1e-12 && u.OffsetToBase == 0);

                if (kind == AppreciateValueKind.Money)
                {
                    // Waehrungen tragen alle den Faktor 1, weil sie nicht umgerechnet werden.
                    Assert.IsTrue(basisCount >= 1, $"{kind} hat keine Basiseinheit.");
                    continue;
                }

                Assert.AreEqual(1, basisCount,
                    $"{kind} braucht genau eine Basiseinheit mit Faktor 1.");
            }
        }

        // ── Umrechnung ────────────────────────────────────────────────────

        [TestMethod]
        [DataRow(AppreciateUnit.Kilometer, 1.0, 1000.0)]
        [DataRow(AppreciateUnit.Meile, 1.0, 1609.344)]
        [DataRow(AppreciateUnit.Seemeile, 1.0, 1852.0)]
        [DataRow(AppreciateUnit.Fuss, 1.0, 0.3048)]
        [DataRow(AppreciateUnit.Zoll, 1.0, 0.0254)]
        [DataRow(AppreciateUnit.Quadratkilometer, 1.0, 1_000_000.0)]
        [DataRow(AppreciateUnit.Hektar, 1.0, 10_000.0)]
        [DataRow(AppreciateUnit.Karat, 1.0, 0.2)]
        [DataRow(AppreciateUnit.Kubikmeter, 1.0, 1000.0)]
        [DataRow(AppreciateUnit.Hektoliter, 1.0, 100.0)]
        [DataRow(AppreciateUnit.Wochen, 1.0, 604800.0)]
        [DataRow(AppreciateUnit.Knoten, 1.0, 0.5144444444)]
        [DataRow(AppreciateUnit.Pferdestaerken, 1.0, 735.49875)]
        [DataRow(AppreciateUnit.Kilowattstunden, 1.0, 3600.0)]
        [DataRow(AppreciateUnit.Kilokalorien, 1.0, 4.184)]
        [DataRow(AppreciateUnit.Gigabyte, 1.0, 1000.0)]
        public void UnitsConvertToTheirBase(AppreciateUnit unit, double value, double expected)
        {
            Assert.AreEqual(expected, AppreciateUnits.ToBase(value, unit), expected * 1e-9 + 1e-9);
        }

        [TestMethod]
        public void KilometerPerHourIsSlowerThanMeterPerSecond()
        {
            Assert.AreEqual(1d, AppreciateUnits.ToBase(3.6, AppreciateUnit.KilometerProStunde), 1e-9,
                "3,6 km/h sind genau 1 m/s.");
        }

        [TestMethod]
        public void FromBaseIsTheOppositeOfToBase()
        {
            foreach (var info in AppreciateUnits.All)
            {
                var round = AppreciateUnits.FromBase(AppreciateUnits.ToBase(42, info.Unit), info.Unit);

                Assert.AreEqual(42d, round, 1e-6, $"{info.Unit} rechnet nicht sauber zurueck.");
            }
        }

        // ── Temperatur: der Fall, bei dem ein Faktor allein falsch waere ──

        [TestMethod]
        [DataRow(0.0, 0.0)]
        [DataRow(100.0, 100.0)]
        [DataRow(-40.0, -40.0)]
        public void CelsiusIsTheBase(double value, double expected)
        {
            Assert.AreEqual(expected, AppreciateUnits.ToBase(value, AppreciateUnit.GradCelsius), 1e-9);
        }

        [TestMethod]
        [DataRow(273.15, 0.0)]
        [DataRow(373.15, 100.0)]
        [DataRow(0.0, -273.15)]
        public void KelvinNeedsTheOffset(double kelvin, double expectedCelsius)
        {
            Assert.AreEqual(expectedCelsius, AppreciateUnits.ToBase(kelvin, AppreciateUnit.Kelvin), 1e-9);
        }

        [TestMethod]
        [DataRow(32.0, 0.0)]
        [DataRow(212.0, 100.0)]
        [DataRow(-40.0, -40.0)]
        public void FahrenheitNeedsOffsetAndFactor(double fahrenheit, double expectedCelsius)
        {
            Assert.AreEqual(expectedCelsius,
                AppreciateUnits.ToBase(fahrenheit, AppreciateUnit.GradFahrenheit), 1e-9);
        }

        /// <summary>
        /// Bei einem Abstand faellt der Versatz weg: zwischen 10 und 20 Grad Celsius liegen
        /// 10 Grad, und zwischen 283,15 und 293,15 Kelvin liegen ebenfalls 10.
        /// </summary>
        [TestMethod]
        public void ADifferenceIgnoresTheOffset()
        {
            Assert.AreEqual(10d, AppreciateUnits.DifferenceFromBase(10, AppreciateUnit.Kelvin), 1e-9);
            Assert.AreEqual(18d, AppreciateUnits.DifferenceFromBase(10, AppreciateUnit.GradFahrenheit), 1e-9,
                "10 Grad Celsius Unterschied sind 18 Grad Fahrenheit Unterschied.");
        }

        // ── Waehrungen ────────────────────────────────────────────────────

        [TestMethod]
        public void CurrenciesAreMarkedAsNotConvertible()
        {
            foreach (var info in AppreciateUnits.For(AppreciateValueKind.Money))
            {
                Assert.IsFalse(info.Convertible,
                    $"{info.Unit} darf nicht als umrechenbar gelten - es gibt keinen festen Kurs.");
            }
        }

        [TestMethod]
        public void EverythingElseIsConvertible()
        {
            foreach (var info in AppreciateUnits.All.Where(u => u.Kind != AppreciateValueKind.Money))
            {
                Assert.IsTrue(info.Convertible, $"{info.Unit} sollte umrechenbar sein.");
            }
        }

        // ── Vorauswahl: die erste Einheit der Liste ───────────────────────

        [TestMethod]
        public void TheDefaultUnitIsTheFirstOfItsList()
        {
            foreach (var kind in Enum.GetValues<AppreciateValueKind>())
            {
                Assert.AreEqual(AppreciateUnits.For(kind)[0].Unit, AppreciateUnits.DefaultUnitFor(kind),
                    $"Die Vorauswahl von {kind} muss die erste Einheit ihrer Liste sein.");
            }
        }

        [TestMethod]
        [DataRow(AppreciateValueKind.Length, AppreciateUnit.Meter)]
        [DataRow(AppreciateValueKind.Mass, AppreciateUnit.Kilogramm)]
        [DataRow(AppreciateValueKind.Volume, AppreciateUnit.Liter)]
        [DataRow(AppreciateValueKind.Duration, AppreciateUnit.Jahre)]
        [DataRow(AppreciateValueKind.Area, AppreciateUnit.Quadratmeter)]
        [DataRow(AppreciateValueKind.Temperature, AppreciateUnit.GradCelsius)]
        [DataRow(AppreciateValueKind.Speed, AppreciateUnit.KilometerProStunde)]
        [DataRow(AppreciateValueKind.Power, AppreciateUnit.Pferdestaerken)]
        [DataRow(AppreciateValueKind.Energy, AppreciateUnit.Kilokalorien)]
        [DataRow(AppreciateValueKind.DataVolume, AppreciateUnit.Gigabyte)]
        [DataRow(AppreciateValueKind.Money, AppreciateUnit.Euro)]
        public void TheDefaultsAreTheOnesAQuizActuallyUses(
            AppreciateValueKind kind, AppreciateUnit expected)
        {
            Assert.AreEqual(expected, AppreciateUnits.DefaultUnitFor(kind));
        }

        // ── Abstand in der Einheit der Frage ──────────────────────────────

        private static AppreciateQestion Question(
            AppreciateValueKind kind, AppreciateUnit unit, double expected)
            => new() { ValueKind = kind, Unit = unit, ExpectedValue = expected };

        /// <summary>
        /// Hielt frueher den Abstand in der Basiseinheit fest: bei 2 km Sollwert und 1,9 km
        /// Tipp stand "Abstand 100" da - das waren 100 Meter, gelesen wurde 100 Kilometer.
        /// </summary>
        [TestMethod]
        public void TheDistanceIsReportedInTheQuestionsOwnUnit()
        {
            var question = Question(AppreciateValueKind.Length, AppreciateUnit.Kilometer, 2);
            var player = Guid.NewGuid();

            var outcome = AppreciateEvaluator.Evaluate(question,
                new Dictionary<Guid, string?> { [player] = "1,9" });

            var guess = outcome.Guesses.Single();

            Assert.AreEqual(100d, guess.Distance!.Value, 1e-9, "Intern bleibt der Abstand in Metern.");
            StringAssert.Contains(AppreciateEvaluator.DescribeDistance(question, guess), "0,1");
            StringAssert.Contains(AppreciateEvaluator.DescribeDistance(question, guess), "km");
        }

        [TestMethod]
        public void AnExactHitIsNamedAsSuch()
        {
            var question = Question(AppreciateValueKind.Number, AppreciateUnit.Stueck, 500);
            var player = Guid.NewGuid();

            var outcome = AppreciateEvaluator.Evaluate(question,
                new Dictionary<Guid, string?> { [player] = "500" });

            Assert.AreEqual("genau richtig",
                AppreciateEvaluator.DescribeDistance(question, outcome.Guesses.Single()));
        }

        [TestMethod]
        public void AnUnreadableGuessIsNamedAsSuch()
        {
            var question = Question(AppreciateValueKind.Number, AppreciateUnit.Stueck, 500);
            var player = Guid.NewGuid();

            var outcome = AppreciateEvaluator.Evaluate(question,
                new Dictionary<Guid, string?> { [player] = "weiss nicht" });

            Assert.AreEqual("nicht lesbar",
                AppreciateEvaluator.DescribeDistance(question, outcome.Guesses.Single()));
        }

        [TestMethod]
        public void ADateDistanceIsGivenInDays()
        {
            var question = new AppreciateQestion
            {
                ValueKind = AppreciateValueKind.Date,
                Unit = AppreciateUnit.Datum,
                ExpectedDate = new DateTime(2000, 1, 1),
            };

            var player = Guid.NewGuid();
            var outcome = AppreciateEvaluator.Evaluate(question,
                new Dictionary<Guid, string?> { [player] = "2000-01-11" });

            StringAssert.Contains(
                AppreciateEvaluator.DescribeDistance(question, outcome.Guesses.Single()), "10 Tage");
        }

        [TestMethod]
        public void ASingleDayIsSaidInSingular()
        {
            var question = new AppreciateQestion
            {
                ValueKind = AppreciateValueKind.Date,
                Unit = AppreciateUnit.Datum,
                ExpectedDate = new DateTime(2000, 1, 1),
            };

            var player = Guid.NewGuid();
            var outcome = AppreciateEvaluator.Evaluate(question,
                new Dictionary<Guid, string?> { [player] = "2000-01-02" });

            Assert.AreEqual("1 Tag daneben",
                AppreciateEvaluator.DescribeDistance(question, outcome.Guesses.Single()));
        }

        // ── Die neuen Groessen im Einsatz ─────────────────────────────────

        [TestMethod]
        public void TheClosestTemperatureWins()
        {
            var question = Question(AppreciateValueKind.Temperature, AppreciateUnit.GradCelsius, 464);
            var anna = Guid.NewGuid();
            var bert = Guid.NewGuid();

            var outcome = AppreciateEvaluator.Evaluate(question,
                new Dictionary<Guid, string?> { [anna] = "400", [bert] = "480" });

            CollectionAssert.AreEqual(new[] { bert }, outcome.WinnerIds.ToArray());
        }

        [TestMethod]
        public void TheClosestAreaWins()
        {
            var question = Question(AppreciateValueKind.Area, AppreciateUnit.Quadratkilometer, 536);
            var anna = Guid.NewGuid();
            var bert = Guid.NewGuid();

            var outcome = AppreciateEvaluator.Evaluate(question,
                new Dictionary<Guid, string?> { [anna] = "500", [bert] = "900" });

            CollectionAssert.AreEqual(new[] { anna }, outcome.WinnerIds.ToArray());
        }

        [TestMethod]
        public void TheBrowserGetsANumberFieldForEveryKindExceptDate()
        {
            foreach (var kind in Enum.GetValues<AppreciateValueKind>())
            {
                var expected = kind == AppreciateValueKind.Date ? "date" : "number";

                Assert.AreEqual(expected, AppreciateEvaluator.InputTypeFor(kind), $"{kind}");
            }
        }

        [TestMethod]
        public void ThePlaceholderNamesTheUnitWhereThereIsOne()
        {
            StringAssert.Contains(
                AppreciateEvaluator.PlaceholderFor(AppreciateValueKind.Speed, AppreciateUnit.Knoten),
                "Knoten");

            Assert.AreEqual("Zahl",
                AppreciateEvaluator.PlaceholderFor(AppreciateValueKind.Number, AppreciateUnit.Stueck));
        }
    }
}
