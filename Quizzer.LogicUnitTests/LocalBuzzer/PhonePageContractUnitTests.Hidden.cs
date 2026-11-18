using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.RegularExpressions;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// Das <c>hidden</c>-Attribut gegen die Stilvorlagen (M37 P6, R2-2): der Browser versteckt ein Element nur über
    /// seine eigene Regel <c>[hidden]{display:none}</c>, und jede Klasse mit <c>display:</c> schlägt sie. Die
    /// Node-Tests sehen nur <c>.hidden === true</c> - was das Telefon zeigt, sieht erst diese Prüfung.
    /// </summary>
    public partial class PhonePageContractUnitTests
    {
        /// <summary>
        /// Wo die Klasse eines umgeschalteten Elements nicht in derselben Datei steht (Parameter, Zerlegung):
        /// Datei und Bezeichner, daneben die Klasse und woher sie kommt.
        /// </summary>
        private static readonly Dictionary<(string Datei, string Name), string> BekannteZiele = new()
        {
            [("abstimmung.js", "ziel")] = "team-abstimmung", // team.js reicht den Teil "team-abstimmung" herein
            [("team.js", "ziel")] = "team-banner",            // index.js: dom.team, index.html class="team-banner"
            [("team.js", "sprecher")] = "team-sprecher",      // teile(ziel) liefert den Teil "team-sprecher"
        };

        private sealed record CssRegel(IReadOnlyList<string> Selektoren, string Inhalt);

        /// <summary>
        /// Jede Klasse, deren Element das JavaScript über <c>.hidden</c> umschaltet und die in <c>styles/</c> ein
        /// <c>display:</c> trägt, ist abgedeckt - durch die Regel <c>[hidden]{display:none!important}</c> oder eine
        /// eigene <c>.klasse[hidden]</c>-Regel. Ohne das sieht jeder Nicht-Läufer Eingabe und „✓ Abgeben".
        /// </summary>
        [TestMethod]
        public void EveryClassTheScriptHidesIsReallyHidden()
        {
            var regeln = CssRegeln(ReadStyles());
            var allgemein = regeln.Any(r => r.Selektoren.Contains("[hidden]")
                && Regex.IsMatch(r.Inhalt, @"display\s*:\s*none\s*!important"));

            var mitDisplay = HiddenUmgeschalteteKlassen()
                .Where(k => regeln.Any(r => IstSubjekt(r, k.Klasse) && SetztDisplay(r)))
                .ToList();

            Assert.IsTrue(mitDisplay.Count > 0,
                "Keine umgeschaltete Klasse traegt display: - dann misst die Zusicherung nichts.");

            var offen = mitDisplay
                .Where(k => !allgemein && !regeln.Any(r => r.Selektoren.Contains($".{k.Klasse}[hidden]")
                    && Regex.IsMatch(r.Inhalt, @"display\s*:\s*none")))
                .Select(k => $"{k.Klasse} ({k.Datei})")
                .Distinct()
                .ToList();

            Assert.AreEqual(0, offen.Count,
                "Diese Klassen setzen display: und schlagen damit das hidden-Attribut - das Telefon zeigt sie "
                + "trotzdem: " + string.Join(", ", offen));
        }

        /// <summary>
        /// Jedes mit <c>.hidden</c> umgeschaltete Element hat eine bestimmbare Klasse - sonst prüfte die Zusicherung
        /// darüber still weniger, als im Skript steht.
        /// </summary>
        [TestMethod]
        public void EveryElementTheScriptHidesHasAKnownClass()
        {
            var unbekannt = HiddenFundstellen()
                .Where(f => KlassenVon(f.Datei, f.Inhalt, f.Name).Count == 0)
                .Select(f => $"{f.Datei}: {f.Name}")
                .Distinct()
                .ToList();

            Assert.AreEqual(0, unbekannt.Count,
                "Fuer diese umgeschalteten Elemente ist keine Klasse bestimmbar - in BekannteZiele eintragen: "
                + string.Join(", ", unbekannt));

            var veraltet = BekannteZiele.Keys
                .Where(z => !HiddenFundstellen().Any(f => f.Datei == z.Datei && f.Name == z.Name))
                .ToList();

            Assert.AreEqual(0, veraltet.Count,
                "BekannteZiele nennt Stellen, die es nicht mehr gibt: " + string.Join(", ", veraltet));
        }

        private static IEnumerable<(string Datei, string Inhalt, string Name)> HiddenFundstellen()
        {
            foreach (var pfad in ScriptFiles())
            {
                var inhalt = File.ReadAllText(pfad);

                foreach (Match treffer in Regex.Matches(inhalt, @"(?:this\.)?\b([A-Za-z_]\w*)\.hidden\s*=(?!=)"))
                    yield return (Path.GetFileName(pfad), inhalt, treffer.Groups[1].Value);
            }
        }

        private static IEnumerable<(string Datei, string Klasse)> HiddenUmgeschalteteKlassen() =>
            HiddenFundstellen()
                .SelectMany(f => KlassenVon(f.Datei, f.Inhalt, f.Name).Select(k => (f.Datei, k)))
                .Distinct();

        /// <summary>Die Klasse eines Bezeichners: aus <c>name.className = "…"</c>, aus <c>name = ….querySelector(".…")</c>
        /// oder aus <see cref="BekannteZiele"/>.</summary>
        private static List<string> KlassenVon(string datei, string inhalt, string name)
        {
            var n = Regex.Escape(name);
            var klassen = Regex.Matches(inhalt, $@"(?:this\.)?\b{n}\.className\s*=\s*""([^""]+)""")
                .SelectMany(m => m.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Concat(Regex.Matches(inhalt, $@"(?:this\.)?\b{n}\s*=\s*[^;=]*querySelector\(\s*""\.([\w-]+)""")
                    .Select(m => m.Groups[1].Value))
                .ToList();

            if (BekannteZiele.TryGetValue((datei, name), out var bekannt))
                klassen.Add(bekannt);

            return klassen.Distinct().ToList();
        }

        private static List<CssRegel> CssRegeln(string styles)
        {
            var ohneKommentare = Regex.Replace(styles, @"/\*.*?\*/", "", RegexOptions.Singleline);

            return Regex.Matches(ohneKommentare, @"([^{}]+)\{([^{}]*)\}")
                .Select(m => new CssRegel(
                    m.Groups[1].Value.Split(',').Select(s => Regex.Replace(s.Trim(), @"\s+", " ")).ToList(),
                    m.Groups[2].Value))
                .ToList();
        }

        /// <summary>Die Klasse steht im letzten Glied eines Selektors - <c>.a input</c> stilt das Kind, nicht <c>.a</c>.</summary>
        private static bool IstSubjekt(CssRegel regel, string klasse) =>
            regel.Selektoren.Any(s =>
            {
                var letztes = Regex.Split(s, @"[\s>+~]+").Last();
                return !letztes.Contains("[hidden]") && Regex.IsMatch(letztes, $@"\.{Regex.Escape(klasse)}(?![\w-])");
            });

        private static bool SetztDisplay(CssRegel regel) =>
            Regex.IsMatch(regel.Inhalt, @"(?<![\w-])display\s*:\s*(?!none\b)[\w-]+");
    }
}
