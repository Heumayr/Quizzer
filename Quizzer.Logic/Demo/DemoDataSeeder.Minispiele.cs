using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.Logic.Demo
{
    /// <summary>
    /// Die Minispiele der Typenschau (M35 B2) - eigene Teildatei, weil die Typenschau sonst ueber die
    /// Groessen-Obergrenze wuechse. Mehrheit und Reaktion im Raster; Minderheit (B3), Tippsprint und
    /// Stoppuhr (C1), Duell, Raeuber und Teilen (C2), Zocker und Gluecksrad (C4), Kopfrechnen, Memory, Blitzrunde und Buchstabensalat (C5), Erklärbär, Bluff und Heiße Kartoffel (C6), Tauziehen (M37 T9), Stellvertreter-Duell (T10), Staffel (T11) im Vorrat - das Raster hat zwei Zeilen.
    /// </summary>
    public static partial class DemoDataSeeder
    {
        /// <summary>Zeilen im Raster der Typenschau - mehr Fragen eines Typs liegen nur im Vorrat (M35 B3).</summary>
        private const int TypenschauZeilen = 2;

        private static IEnumerable<QuestionBase> TypenschauMinispiele(Guid kat)
        {
            yield return Mehrheit(kat, "Typenschau: Mehrheit", "MIN-1", 100, Difficulty.Level1,
                "Was isst die Runde lieber?", variante: 0,
                ["Pizza", "Pasta", "Sushi", "Schnitzel"]);

            yield return OhneZeilen(kat, "Typenschau: Reaktion", "MIN-3", 200, Difficulty.Level2,
                "Wer ist am schnellsten? Erst bei Grün tippen!", MinigameKind.Reaction);

            // Nur im Vorrat: das Raster hat zwei Zeilen (TypenschauZeilen), die Reaktion hat die zweite.
            yield return Mehrheit(kat, "Typenschau: Minderheit", "MIN-2", 200, Difficulty.Level2,
                "Wohin ginge die Runde am liebsten? (Hier gewinnt die kleinste Gruppe.)",
                variante: MehrheitEvaluator.MinorityVariant,
                ["Berge", "Meer", "Stadt"]);

            // M35 C1, ebenfalls nur im Vorrat.
            yield return OhneZeilen(kat, "Typenschau: Tippsprint", "MIN-4", 200, Difficulty.Level2,
                "Wer tippt am häufigsten? Zehn Sekunden, so schnell ihr könnt!", MinigameKind.TapSprint);

            yield return OhneZeilen(kat, "Typenschau: Stoppuhr", "MIN-5", 200, Difficulty.Level2,
                "Wer hat das beste Zeitgefühl? Stoppt nach genau zehn Sekunden!", MinigameKind.Stopwatch);

            // M35 C2, ebenfalls nur im Vorrat: die Spiele mit Spielerwahl.
            yield return OhneZeilen(kat, "Typenschau: Duell", "MIN-6", 200, Difficulty.Level2,
                "Wer das Feld aufgedeckt hat, fordert jemanden heraus: Schere, Stein, Papier!", MinigameKind.Duel);

            yield return OhneZeilen(kat, "Typenschau: Räuber und Wächter", "MIN-7", 200, Difficulty.Level2,
                "Bestehlen oder Schild? Alle wählen geheim und gleichzeitig.", MinigameKind.RobberAndGuard);

            yield return OhneZeilen(kat, "Typenschau: Teilen oder Nehmen", "MIN-8", 200, Difficulty.Level2,
                "Teilen oder nehmen? Zwei entscheiden geheim über die Punkte des Felds.", MinigameKind.ShareOrTake);

            // M35 C4, ebenfalls nur im Vorrat.
            yield return OhneZeilen(kat, "Typenschau: Zocker", "MIN-9", 200, Difficulty.Level2,
                "Wer traut sich am längsten? Steigt aus, bevor es abstürzt!", MinigameKind.Crash);

            yield return OhneZeilen(kat, "Typenschau: Glücksrad", "MIN-10", 200, Difficulty.Level2,
                "Das Glücksrad! Wer das Feld aufgedeckt hat, dreht – Verdoppeln, Klauen, Verschenken, Pech oder Robin Hood.",
                MinigameKind.WheelOfFortune);

            // M35 C5, ebenfalls nur im Vorrat.
            yield return OhneZeilen(kat, "Typenschau: Kopfrechnen", "MIN-11", 200, Difficulty.Level2,
                "Wer rechnet am schnellsten? Eine Minute, so viele Aufgaben wie möglich!", MinigameKind.MentalArithmetic);

            yield return OhneZeilen(kat, "Typenschau: Memory", "MIN-12", 200, Difficulty.Level2,
                "Wer findet zuerst alle Paare? Jeder auf seinem Telefon, alle dieselbe Belegung.", MinigameKind.Memory);

            yield return MitZeilen(kat, "Typenschau: Blitzrunde", "MIN-13", 200, Difficulty.Level2,
                "Wahr oder falsch? Eine Minute, eine Aussage nach der anderen!", MinigameKind.LightningRound,
                [
                    ("Der Mount Everest ist der höchste Berg der Erde.", true),
                    ("Spinnen haben sechs Beine.", false),
                    ("Die Donau fließt durch Wien.", true),
                    ("Ein Marathon ist genau 40 Kilometer lang.", false),
                    ("Kängurus leben in Australien.", true),
                    ("Gold ist schwerer als Blei.", true),
                    ("Der Mond leuchtet aus eigener Kraft.", false),
                    ("Ein Schaltjahr hat 366 Tage.", true),
                ]);

            yield return MitZeilen(kat, "Typenschau: Buchstabensalat", "MIN-14", 200, Difficulty.Level2,
                "Wer entwirrt die Begriffe zuerst? Alle sehen dasselbe Wirrwarr!", MinigameKind.WordScramble,
                [
                    ("Käse", false),
                    ("Schmetterling", false),
                    ("Gießkanne", false),
                    ("Fußball", false),
                    ("Regenbogen", false),
                    ("Eichhörnchen", false),
                ]);

            yield return MitZeilen(kat, "Typenschau: Erklärbär", "MIN-15", 300, Difficulty.Level2,
                "Einer erklärt, alle anderen raten – aber das Wort selbst ist verboten!", MinigameKind.ExplainIt,
                [
                    ("Leuchtturm", false),
                    ("Zahnbürste", false),
                    ("Achterbahn", false),
                    ("Kaffeemaschine", false),
                ]);

            yield return MitZeilen(kat, "Typenschau: Bluff", "MIN-16", 300, Difficulty.Level3,
                "Was ist ein „Kalumet“? Erfindet eine Antwort, die echt klingt!", MinigameKind.Bluff,
                [
                    ("eine Friedenspfeife", false),
                    ("Friedenspfeife", false),
                ]);

            yield return MitZeilen(kat, "Typenschau: Heiße Kartoffel", "MIN-17", 300, Difficulty.Level2,
                "Wer sie hält, muss richtig antworten, um sie loszuwerden – bevor sie platzt!", MinigameKind.HotPotato,
                [
                    ("Der Mond ist ein Planet.", false),
                    ("Spinnen haben acht Beine.", true),
                    ("Wale sind Säugetiere.", true),
                    ("Der Rhein mündet ins Schwarze Meer.", false),
                    ("Tomaten sind botanisch gesehen Beeren.", true),
                    ("Der Eiffelturm steht in Rom.", false),
                    ("Pinguine leben am Nordpol.", false),
                    ("Bienen können tanzen, um den Weg zu zeigen.", true),
                ]);

            // M37 T9: nur im Teamspiel - die Typenschau ist keins, also nur im Vorrat; gespielt im Teamabend (T12).
            yield return OhneZeilen(kat, "Typenschau: Tauziehen", "MIN-18", 300, Difficulty.Level2,
                "Team gegen Team: Wer zieht das Seil auf seine Seite? Tippen, so schnell ihr könnt!", MinigameKind.TugOfWar);

            // M37 T10: ebenso nur im Teamspiel.
            yield return OhneZeilen(kat, "Typenschau: Stellvertreter-Duell", "MIN-19", 300, Difficulty.Level2,
                "Jedes Team schickt einen Stellvertreter – wer reagiert am schnellsten für sein Team?", MinigameKind.ProxyDuel);

            // M37 T11: ebenso nur im Teamspiel; die Zeilen sind die Teile in ihrer Reihenfolge.
            yield return MitZeilen(kat, "Typenschau: Staffel", "MIN-20", 300, Difficulty.Level2,
                "Die Planeten unseres Sonnensystems von innen nach außen – reihum je einer, ein Fehler gibt ans nächste Team weiter!",
                MinigameKind.Relay,
                [
                    ("Merkur", false),
                    ("Venus", false),
                    ("Erde", false),
                    ("Mars", false),
                    ("Jupiter", false),
                    ("Saturn", false),
                    ("Uranus", false),
                    ("Neptun", false),
                ]);
        }

        /// <summary>Arten mit Inhalt (Blitzrunde, Buchstabensalat, M35 C5; Erklärbär, C6): je Zeile ein Schritt, bei der Blitzrunde heisst der Haken „wahr".</summary>
        private static QuestionBase MitZeilen(
            Guid kategorieId, string bezeichnung, string kurz, int punkte, Difficulty stufe, string frageText,
            MinigameKind art, (string Text, bool Wahr)[] zeilen)
        {
            var frage = OhneZeilen(kategorieId, bezeichnung, kurz, punkte, stufe, frageText, art);

            for (var i = 0; i < zeilen.Length; i++)
                frage.Steps.Add(Schritt(frage.Id, (i + 1) * 10, zeilen[i].Text, istLoesung: zeilen[i].Wahr));

            return frage;
        }

        /// <summary>Arten im Telefonrahmen (Reaktion B3, Tippsprint und Stoppuhr C1): keine Zeilen, nur Art und Parameter.</summary>
        private static QuestionBase OhneZeilen(
            Guid kategorieId, string bezeichnung, string kurz, int punkte, Difficulty stufe, string frageText,
            MinigameKind art)
        {
            var frage = new MinigameQuestion();
            Grunddaten(frage, kategorieId, bezeichnung, kurz, punkte, stufe);
            MinigameProfiles.For(art).ApplyDefaults(frage);
            frage.MinusPoints = 0;
            frage.QuestionText = frageText;

            return frage;
        }

        /// <summary>Mehrheit: je Option ein Schritt, kein Haken - richtig ist, was die Gruppe waehlt.</summary>
        private static QuestionBase Mehrheit(
            Guid kategorieId, string bezeichnung, string kurz, int punkte, Difficulty stufe,
            string frageText, int variante, string[] optionen)
        {
            var frage = new MinigameQuestion();
            Grunddaten(frage, kategorieId, bezeichnung, kurz, punkte, stufe);
            frage.MinusPoints = 0;
            frage.QuestionText = frageText;
            frage.ShowTextOnKeySelect = true;
            frage.Kind = MinigameKind.Majority;
            frage.Variant = variante;

            for (var i = 0; i < optionen.Length; i++)
                frage.Steps.Add(Schritt(frage.Id, (i + 1) * 10, optionen[i]));

            frage.BuzzerMaxAllowedKeySelect = 1;

            return frage;
        }
    }
}
