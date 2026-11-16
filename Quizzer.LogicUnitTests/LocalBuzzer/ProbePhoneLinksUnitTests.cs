using LocalBuzzer.Service.Base;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Base;
using System.Web;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// Probetelefone am PC (M35 B3, V1): je Mitspieler eine Adresse mit seinem abgeleiteten Schluessel -
    /// nie die rohe Id, die seit M10 kein Ausweis mehr sein darf.
    /// </summary>
    [TestClass]
    public class ProbePhoneLinksUnitTests
    {
        [TestMethod]
        public void EachPlayerGetsHisDerivedKeyAndNeverTheRawId()
        {
            var schluessel = BuzzerAccessKeys.Fluechtig();
            var spieler = new[]
            {
                new Player { Id = Guid.NewGuid(), Designation = "Anna" },
                new Player { Id = Guid.NewGuid(), Designation = "Bert" },
            };

            var links = ProbePhoneLinks.For(spieler, schluessel, "localhost:5000");

            Assert.AreEqual(2, links.Count, "Nicht je Mitspieler ein Probetelefon.");

            foreach (var link in links)
            {
                var text = link.Url.ToString();

                foreach (var form in new[] { "N", "D", "B" })
                    Assert.IsFalse(text.Contains(link.Player.Id.ToString(form), StringComparison.OrdinalIgnoreCase),
                        $"Die rohe Spieler-Id steht in der Adresse: {text}");

                var key = HttpUtility.ParseQueryString(link.Url.Query)["key"];

                Assert.AreSame(link.Player, schluessel.FindeSchluessel(key, spieler, p => p.Id),
                    $"Der Schluessel in {text} gehoert nicht zu {link.Player.Designation}.");
                Assert.AreEqual("localhost", link.Url.Host);
                Assert.AreEqual(5000, link.Url.Port);
            }
        }
    }
}
