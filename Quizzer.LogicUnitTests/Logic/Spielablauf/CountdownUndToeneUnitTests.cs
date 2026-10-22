using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Anzeige;
using Quizzer.Logic.Spielablauf;

namespace Quizzer.LogicUnitTests.Logic.Spielablauf
{
    [TestClass]
    public class CountdownUndToeneUnitTests
    {
        private static readonly DateTime T0 = new(2026, 10, 22, 20, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void TheCountdownRoundsUpSoZeroOnlyShowsWhenTimeIsUp()
        {
            var c = new Countdown(TimeSpan.FromSeconds(10), T0);

            Assert.AreEqual(10, c.Sekunden(T0));
            Assert.AreEqual(10, c.Sekunden(T0.AddMilliseconds(800)));
            Assert.AreEqual(9, c.Sekunden(T0.AddSeconds(1)));
            Assert.AreEqual(1, c.Sekunden(T0.AddMilliseconds(9_900)));
            Assert.IsFalse(c.Abgelaufen(T0.AddMilliseconds(9_900)));
            Assert.AreEqual(0, c.Sekunden(T0.AddSeconds(10)));
            Assert.IsTrue(c.Abgelaufen(T0.AddSeconds(10)));
            Assert.AreEqual(0, c.Sekunden(T0.AddSeconds(30)), "Nach dem Ende wird nicht negativ gezählt.");
        }

        [TestMethod]
        public void TheRingAndTheWarning()
        {
            var c = new Countdown(TimeSpan.FromSeconds(20), T0);

            Assert.AreEqual(1.0, c.Anteil(T0), 1e-9);
            Assert.AreEqual(0.5, c.Anteil(T0.AddSeconds(10)), 1e-9);
            Assert.AreEqual(0.0, c.Anteil(T0.AddSeconds(25)), 1e-9);
            Assert.AreEqual(1.0, c.Anteil(T0.AddSeconds(-3)), 1e-9, "Eine vorgehende Uhr macht den Ring nicht größer.");

            Assert.IsFalse(c.Knapp(T0.AddSeconds(14)));
            Assert.IsTrue(c.Knapp(T0.AddSeconds(15)));
            Assert.IsFalse(c.Knapp(T0.AddSeconds(20)), "Abgelaufen ist nicht mehr knapp.");
        }

        [TestMethod]
        public void LongCountdownsShowMinutes()
        {
            var c = new Countdown(TimeSpan.FromSeconds(90), T0);

            Assert.AreEqual("1:30", c.Text(T0));
            Assert.AreEqual("1:00", c.Text(T0.AddSeconds(30)));
            Assert.AreEqual("1:00", c.Text(T0.AddMilliseconds(30_500)), "59,5 s Rest zeigt aufgerundet 1:00.");
            Assert.AreEqual("59", c.Text(T0.AddSeconds(31)));
        }

        [TestMethod]
        public void ACountdownNeedsADuration()
            => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Countdown(TimeSpan.Zero, T0));

        [TestMethod]
        [DataRow("Buzzer")]
        [DataRow("Tick")]
        [DataRow("Ende")]
        public void TheTonesAreValidPcmWavs(string welcher)
        {
            var wav = welcher switch { "Buzzer" => Toene.Buzzer(), "Tick" => Toene.Tick(), _ => Toene.Ende() };

            Assert.AreEqual("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
            Assert.AreEqual("WAVE", System.Text.Encoding.ASCII.GetString(wav, 8, 4));
            Assert.AreEqual(wav.Length - 8, BitConverter.ToInt32(wav, 4), "Die RIFF-Länge stimmt nicht.");
            Assert.AreEqual(1, BitConverter.ToInt16(wav, 20), "kein PCM");
            Assert.AreEqual(Toene.Abtastrate, BitConverter.ToInt32(wav, 24));
            Assert.AreEqual(wav.Length - 44, BitConverter.ToInt32(wav, 40), "Die Datenlänge stimmt nicht.");

            var proben = Enumerable.Range(0, (wav.Length - 44) / 2).Select(i => BitConverter.ToInt16(wav, 44 + i * 2)).ToList();

            Assert.IsTrue(proben.Max(p => Math.Abs((int)p)) > 3000, "Der Ton ist praktisch stumm.");
            Assert.IsTrue(Math.Abs((int)proben[0]) < 200 && Math.Abs((int)proben[^1]) < 600, "Anfang oder Ende knackt (kein weicher Übergang).");
        }
    }
}
