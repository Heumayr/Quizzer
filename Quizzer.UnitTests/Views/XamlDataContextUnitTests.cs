using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Text.RegularExpressions;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// P11-2 (Durchsicht M37, P12): ein ViewModel, das das XAML als DataContext anlegt, hängt sich an <c>Loaded</c> und
    /// <c>Closed</c> des Fensters. Reicht der Öffner danach ein anderes herein, laufen beide - <c>WindowBase</c> löst das erste nie.
    /// Im Fragefenster räumte das zweite so die Buzzer-Steuerung ab, bevor gebucht war. Gelesen wird der Quelltext: welches XAML
    /// ein ViewModel anlegt, und welcher Öffner den DataContext ersetzt. Beides zusammen ist rot, es sei denn, das ViewModel tut
    /// nachweislich nichts - dann steht es mit Grund in <see cref="ErsetztOhneWirkung"/>.
    /// </summary>
    [TestClass]
    public class XamlDataContextUnitTests
    {
        /// <summary>Fenster, deren XAML-ViewModel ersetzt wird und dabei nichts tut - je mit dem Grund, gelesen am Quelltext.</summary>
        private static readonly Dictionary<string, string> ErsetztOhneWirkung = new()
        {
            ["GamePlayerView"] = "GamePlayerViewModel: kein Konstruktor, kein OnClosed; OnloadAsync meldet nur Eigenschaften (P11-2)",
            ["PromptView"] = "PromptViewModel: keine ViewModelBase, also an keinem Fensterereignis; der Konstruktor setzt nur Texte (P11-2)",
        };

        /// <summary>Wo der Öffner den DataContext heute ersetzt - der Leser muss sie alle finden, sonst misst die Zusicherung nichts.</summary>
        private static readonly string[] BekannteOeffner =
            ["MainWindow", "QuestionMasterView", "GameMasterView", "GamePlayerView", "QuestionSelectorView", "PromptView", "PlayersResultView"];

        [TestMethod]
        public void NoWindowCreatesAViewModelThatItsOpenerReplaces()
        {
            var wurzel = Wurzel();
            var imXaml = ViewModelsImXaml(wurzel);
            var ersetzt = ErsetzteDataContexte(wurzel);

            Assert.IsTrue(imXaml.Count >= 20, $"Nur {imXaml.Count} XAML-ViewModels gefunden - der Leser greift nicht mehr.");
            var nichtGefunden = BekannteOeffner.Except(ersetzt).ToList();
            Assert.AreEqual(0, nichtGefunden.Count, "Der Leser findet diese Öffner nicht mehr: " + string.Join(", ", nichtGefunden));

            var doppelt = imXaml.Keys.Where(ersetzt.Contains).ToList();
            var verboten = doppelt.Where(f => !ErsetztOhneWirkung.ContainsKey(f)).Select(f => $"{f} ({imXaml[f]})").ToList();
            var veraltet = ErsetztOhneWirkung.Keys.Except(doppelt).ToList();

            Assert.AreEqual(0, verboten.Count, "Diese Fenster legen im XAML ein ViewModel an, das ihr Öffner ersetzt - beide hängen an Loaded und "
                + "Closed. d:DataContext nehmen (wie P10-2) oder mit Grund in ErsetztOhneWirkung: " + string.Join(", ", verboten));
            Assert.AreEqual(0, veraltet.Count, "Diese Einträge in ErsetztOhneWirkung treffen nicht mehr zu: " + string.Join(", ", veraltet));
        }

        /// <summary>Je Fenster (Klassenname aus <c>x:Class</c>) das ViewModel, das sein XAML als DataContext-Element anlegt.</summary>
        private static Dictionary<string, string> ViewModelsImXaml(string wurzel)
        {
            var element = new Regex(@"<(?:\w+:)?\w+\.DataContext>\s*<\w+:(?<vm>\w+)[\s/>]");
            var klasse = new Regex(@"x:Class=""(?:[\w\.]+\.)?(?<name>\w+)""");
            var funde = new Dictionary<string, string>();

            foreach (var datei in Dateien(wurzel, "*.xaml"))
            {
                var text = OhneKommentare(File.ReadAllText(datei));

                if (element.Match(text) is { Success: true } treffer && klasse.Match(text) is { Success: true } name)
                    funde[name.Groups["name"].Value] = treffer.Groups["vm"].Value;
            }

            return funde;
        }

        /// <summary>
        /// Klassennamen, deren DataContext ein Öffner im Code setzt: im Objektinitialisierer (<c>new X { DataContext = … }</c>),
        /// über die Variable (<c>x = new X(); x.DataContext = …</c>) oder im eigenen Code-Behind (<c>DataContext = …</c>).
        /// </summary>
        private static HashSet<string> ErsetzteDataContexte(string wurzel)
        {
            var initialisierer = new Regex(@"new\s+(?:[\w\.]+\.)?(?<typ>\w+)\s*(?:\([^)]*\))?\s*\{[^}]*?\bDataContext\s*=(?!=)", RegexOptions.Singleline);
            var variable = new Regex(@"(?<var>\w+)\s*=\s*new\s+(?:[\w\.]+\.)?(?<typ>\w+)\s*[\(\{]");
            var eigenes = new Regex(@"(?<![\.\w])DataContext\s*=(?!=)");
            var klasse = new Regex(@"partial\s+class\s+(?<name>\w+)");
            var funde = new HashSet<string>();

            foreach (var datei in Dateien(wurzel, "*.cs"))
            {
                var text = File.ReadAllText(datei);

                foreach (Match m in initialisierer.Matches(text))
                    funde.Add(m.Groups["typ"].Value);

                foreach (Match m in variable.Matches(text))
                {
                    // Nur kurz dahinter: derselbe Variablenname kommt in anderen Methoden derselben Datei wieder vor.
                    var danach = text.Substring(m.Index, Math.Min(1500, text.Length - m.Index));

                    if (Regex.IsMatch(danach, $@"\b{Regex.Escape(m.Groups["var"].Value)}\.DataContext\s*=(?!=)"))
                        funde.Add(m.Groups["typ"].Value);
                }

                if (datei.EndsWith(".xaml.cs", StringComparison.OrdinalIgnoreCase) && eigenes.IsMatch(text) && klasse.Match(text) is { Success: true } k)
                    funde.Add(k.Groups["name"].Value);
            }

            return funde;
        }

        private static string OhneKommentare(string xaml) => Regex.Replace(xaml, "<!--.*?-->", string.Empty, RegexOptions.Singleline);

        private static IEnumerable<string> Dateien(string wurzel, string muster)
            => Directory.EnumerateFiles(wurzel, muster, SearchOption.AllDirectories)
                .Where(d => !d.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    && !d.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

        /// <summary>Der Quelltext der WPF-Anwendung - gesucht vom Ausgabeverzeichnis aufwärts bis zur Projektmappe.</summary>
        private static string Wurzel()
        {
            var verzeichnis = new DirectoryInfo(AppContext.BaseDirectory);

            while (verzeichnis != null && !File.Exists(Path.Combine(verzeichnis.FullName, "Quizzer.slnx")))
                verzeichnis = verzeichnis.Parent;

            Assert.IsNotNull(verzeichnis, "Quizzer.slnx nicht gefunden.");
            return Path.Combine(verzeichnis.FullName, "Quizzer");
        }
    }
}
