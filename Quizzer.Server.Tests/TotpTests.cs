using Quizzer.Server.Auth;
using System.Text;

namespace Quizzer.Server.Tests
{
    /// <summary>Der TOTP-Kern ohne Server: RFC-Testvektoren, Fenster, Wiederholschutz (M36 A5).</summary>
    [TestClass]
    public sealed class TotpTests
    {
        /// <summary>Das Geheimnis aus RFC 6238 Anhang B und RFC 4226 Anhang D fuer SHA1.</summary>
        private static readonly byte[] RfcSecret = Encoding.ASCII.GetBytes("12345678901234567890");

        [TestMethod]
        [DataRow(59L, "94287082")]
        [DataRow(1111111109L, "07081804")]
        [DataRow(1111111111L, "14050471")]
        [DataRow(1234567890L, "89005924")]
        [DataRow(2000000000L, "69279037")]
        [DataRow(20000000000L, "65353130")]
        public void MatchesTheRfc6238TestVectors(long unixSeconds, string expected)
        {
            var step = Totp.StepAt(DateTimeOffset.FromUnixTimeSeconds(unixSeconds));
            Assert.AreEqual(expected, Totp.Code(RfcSecret, step, digits: 8));
        }

        /// <summary>Sechs Ziffern, wie sie die Apps zeigen - RFC 4226 Anhang D, Zaehler 0 bis 9.</summary>
        [TestMethod]
        public void SixDigitsMatchTheRfc4226Values()
        {
            string[] expected = ["755224", "287082", "359152", "969429", "338314", "254676", "287922", "162583", "399871", "520489"];
            CollectionAssert.AreEqual(expected, Enumerable.Range(0, 10).Select(i => Totp.Code(RfcSecret, i)).ToArray());
        }

        [TestMethod]
        [DataRow("f", "MY")]
        [DataRow("fo", "MZXQ")]
        [DataRow("foo", "MZXW6")]
        [DataRow("foob", "MZXW6YQ")]
        [DataRow("fooba", "MZXW6YTB")]
        [DataRow("foobar", "MZXW6YTBOI")]
        public void Base32FollowsRfc4648(string plain, string encoded)
        {
            Assert.AreEqual(encoded, Totp.ToBase32(Encoding.ASCII.GetBytes(plain)));
            Assert.AreEqual(plain, Encoding.ASCII.GetString(Totp.FromBase32(encoded.ToLowerInvariant())));
        }

        [TestMethod]
        public void TheWindowIsOneStepEachWay()
        {
            var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_015);
            var step = Totp.StepAt(now);

            Assert.AreEqual(step - 1, Totp.Match(RfcSecret, Totp.Code(RfcSecret, step - 1), now, null));
            Assert.AreEqual(step, Totp.Match(RfcSecret, Totp.Code(RfcSecret, step), now, null));
            Assert.AreEqual(step + 1, Totp.Match(RfcSecret, Totp.Code(RfcSecret, step + 1), now, null));
            Assert.IsNull(Totp.Match(RfcSecret, Totp.Code(RfcSecret, step - 2), now, null), "zwei Schritte zurueck");
            Assert.IsNull(Totp.Match(RfcSecret, Totp.Code(RfcSecret, step + 2), now, null), "zwei Schritte voraus");
        }

        [TestMethod]
        public void AStepAtOrBeforeTheLastUsedOneIsRefused()
        {
            var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_015);
            var step = Totp.StepAt(now);

            Assert.IsNull(Totp.Match(RfcSecret, Totp.Code(RfcSecret, step), now, lastStep: step), "derselbe Schritt");
            Assert.IsNull(Totp.Match(RfcSecret, Totp.Code(RfcSecret, step - 1), now, lastStep: step), "ein frueherer Schritt");
            Assert.AreEqual(step + 1, Totp.Match(RfcSecret, Totp.Code(RfcSecret, step + 1), now, lastStep: step));
        }

        [TestMethod]
        public void OnlySixDigitsCountAsACode()
        {
            Assert.IsTrue(Totp.IsCodeFormat("123 456"));
            Assert.IsFalse(Totp.IsCodeFormat("12345"));
            Assert.IsFalse(Totp.IsCodeFormat("1234567"));
            Assert.IsFalse(Totp.IsCodeFormat("12345a"));
            Assert.IsFalse(Totp.IsCodeFormat(null));
            Assert.IsNull(Totp.Match(RfcSecret, "１２３４５６", DateTimeOffset.UtcNow, null), "Ziffern ausserhalb von ASCII");
        }

        [TestMethod]
        public void TheKeyUriCarriesSecretAndIssuer()
        {
            var uri = Totp.KeyUri("Quizzer", "anna maria", "ABCDEFGH");

            StringAssert.StartsWith(uri, "otpauth://totp/Quizzer%3Aanna%20maria?");
            StringAssert.Contains(uri, "secret=ABCDEFGH");
            StringAssert.Contains(uri, "issuer=Quizzer");
            StringAssert.Contains(uri, "digits=6&period=30");
        }
    }
}
