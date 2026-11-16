using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Sonderregeln;

namespace Quizzer.Logic.Demo
{
    /// <summary>
    /// Der Inhalt des Minispielabends (M35 C8): sieben Themen-Spalten zu je vier Zellen - jede Minispiel-Art und jede
    /// Sonderregel mindestens einmal, dazu die Spalte „Aufwärmen" mit gewöhnlichen Fragen, an der die Joker-Runde läuft.
    /// <para>
    /// Eine neue Art oder Regel gehört hier hinein - <c>MinispielabendUnitTests</c> prüft die Vollständigkeit über die
    /// Aufzählungen.
    /// </para>
    /// </summary>
    public static partial class DemoDataSeeder
    {
        /// <summary>Eine Zelle des Minispielabends: die Frage und, bei einer gewöhnlichen, eine Sonderregel.</summary>
        private sealed record AbendZelle(
            QuestionBase Frage, CellSpecialRule Regel = CellSpecialRule.None, bool Angekuendigt = false, int Spielweise = 0);

        /// <summary>Eine Spalte des Minispielabends: das Thema steht im Spaltenkopf.</summary>
        private sealed record AbendSpalte(string Thema, IReadOnlyList<AbendZelle> Zellen);

        /// <summary>Die Spalten, links beginnend; je Zeile 100 Punkte mehr.</summary>
        private static IEnumerable<AbendSpalte> BaueMinispielabend(Guid kat)
        {
            yield return new("Aufwärmen", Aufwaermen(kat));
            yield return new("Flinke Finger", FlinkeFinger(kat));
            yield return new("Köpfchen", Koepfchen(kat));
            yield return new("Wer gegen wen", WerGegenWen(kat));
            yield return new("Glück und Mut", GlueckUndMut(kat));
            yield return new("Worte und Meinungen", WorteUndMeinungen(kat));
            yield return new("Taktik", Taktik(kat));
        }

        private static string Abend(string name) => $"Minispielabend: {name}";

        /// <summary>Gewöhnliche Fragen ohne Sonderregel - nur dort gibt es die Joker-Runde (C7b).</summary>
        private static List<AbendZelle> Aufwaermen(Guid kat) =>
        [
            new(Standard(kat, Abend("Hauptstadt von Kanada"), "Kanada", 100, Difficulty.Level0,
                "Wie heißt die Hauptstadt von Kanada?",
                ["Sie liegt in der Provinz Ontario.", "Es ist nicht Toronto."],
                "Ottawa")),
            new(MultipleChoice(kat, Abend("Herzen des Oktopus"), "Oktopus", 200, Difficulty.Level0,
                "Wie viele Herzen hat ein Oktopus?",
                [("Drei", true), ("Eins", false), ("Zwei", false), ("Acht", false)],
                "Drei – zwei pumpen das Blut durch die Kiemen, eins durch den Körper.")),
            new(WahrFalsch(kat, Abend("Wahr oder falsch"), "Stimmt das?", 300, Difficulty.Level0,
                "Welche dieser Aussagen sind wahr?",
                [
                    ("Die Zugspitze ist der höchste Berg Deutschlands.", true),
                    ("Der Bodensee grenzt an vier Länder.", false),
                    ("Ein Marathon ist länger als 42 Kilometer.", true),
                    ("Schmetterlinge schmecken mit den Füßen.", true),
                ])),
            new(Schaetzfrage(kat, Abend("Länge der Donau"), "Donau", 400, Difficulty.Level0,
                "Wie viele Kilometer ist die Donau lang?", 2850, AppreciateValueKind.Length, AppreciateUnit.Kilometer,
                "Rund 2850 Kilometer – vom Schwarzwald bis ins Schwarze Meer.")),
        ];

        private static List<AbendZelle> FlinkeFinger(Guid kat) =>
        [
            new(OhneZeilen(kat, Abend("Reaktion"), "Reaktion", 100, Difficulty.Level0,
                "Wer ist am schnellsten? Erst bei Grün tippen!", MinigameKind.Reaction)),
            new(OhneZeilen(kat, Abend("Tippsprint"), "Tippsprint", 200, Difficulty.Level0,
                "Wer tippt am häufigsten? Daumen locker machen!", MinigameKind.TapSprint)),
            new(OhneZeilen(kat, Abend("Stoppuhr"), "Stoppuhr", 300, Difficulty.Level0,
                "Wer hat die innere Uhr am besten gestellt? Stoppt genau bei der Zielzeit!", MinigameKind.Stopwatch)),
            new(OhneZeilen(kat, Abend("Kopfrechnen"), "Kopfrechnen", 400, Difficulty.Level0,
                "Wer rechnet am schnellsten? So viele Aufgaben wie möglich!", MinigameKind.MentalArithmetic)),
        ];

        private static List<AbendZelle> Koepfchen(Guid kat) =>
        [
            new(MitZeilen(kat, Abend("Blitzrunde"), "Blitzrunde", 100, Difficulty.Level0,
                "Wahr oder falsch? Eine Aussage nach der anderen!", MinigameKind.LightningRound,
                [
                    ("Die Erde dreht sich von Westen nach Osten.", true),
                    ("Ein Sechseck hat sieben Ecken.", false),
                    ("Salzburg ist die Geburtsstadt von Mozart.", true),
                    ("Diamanten bestehen aus Kohlenstoff.", true),
                    ("Der Pazifik ist der kleinste Ozean.", false),
                    ("Fledermäuse sind Vögel.", false),
                    ("Ein Fußballspiel dauert regulär 90 Minuten.", true),
                    ("Die Chinesische Mauer ist vom Mond aus mit bloßem Auge zu sehen.", false),
                ])),
            new(MitZeilen(kat, Abend("Buchstabensalat"), "Buchstabensalat", 200, Difficulty.Level0,
                "Wer entwirrt die Begriffe zuerst?", MinigameKind.WordScramble,
                [
                    ("Lebkuchen", false),
                    ("Schneemann", false),
                    ("Kuckucksuhr", false),
                    ("Zitronenfalter", false),
                    ("Märchen", false),
                    ("Brötchen", false),
                ])),
            new(OhneZeilen(kat, Abend("Memory"), "Memory", 300, Difficulty.Level0,
                "Wer findet zuerst alle Paare?", MinigameKind.Memory)),
            new(MitZeilen(kat, Abend("Heiße Kartoffel"), "Heiße Kartoffel", 400, Difficulty.Level0,
                "Wer sie hält, muss richtig antworten, um sie loszuwerden – bevor sie platzt!", MinigameKind.HotPotato,
                [
                    ("Der Mount Everest liegt im Himalaya.", true),
                    ("Pinguine können fliegen.", false),
                    ("Ein Würfel hat sechs Seiten.", true),
                    ("Die Sonne ist ein Planet.", false),
                    ("Ein Jahr hat 54 Wochen.", false),
                    ("Wien liegt an der Donau.", true),
                    ("Eine Woche hat 168 Stunden.", true),
                    ("Tomaten gehören zu den Nachtschattengewächsen.", true),
                ])),
        ];

        private static List<AbendZelle> WerGegenWen(Guid kat) =>
        [
            new(OhneZeilen(kat, Abend("Duell"), "Duell", 100, Difficulty.Level0,
                "Wer das Feld aufgedeckt hat, fordert jemanden heraus!", MinigameKind.Duel)),
            new(OhneZeilen(kat, Abend("Räuber und Wächter"), "Räuber und Wächter", 200, Difficulty.Level0,
                "Bestehlen oder Schild? Alle wählen geheim und gleichzeitig.", MinigameKind.RobberAndGuard)),
            new(OhneZeilen(kat, Abend("Teilen oder Nehmen"), "Teilen oder Nehmen", 300, Difficulty.Level0,
                "Teilen oder nehmen? Zwei entscheiden geheim über die Punkte des Felds.", MinigameKind.ShareOrTake)),
            new(MultipleChoice(kat, Abend("Kopfgeld"), "Fußball", 400, Difficulty.Level0,
                "Wie viele Spieler einer Mannschaft stehen beim Fußball gleichzeitig auf dem Feld?",
                [("Elf", true), ("Zehn", false), ("Zwölf", false), ("Neun", false)],
                "Elf – zehn Feldspieler und der Torwart."),
                CellSpecialRule.Bounty),
        ];

        private static List<AbendZelle> GlueckUndMut(Guid kat) =>
        [
            new(OhneZeilen(kat, Abend("Zocker"), "Zocker", 100, Difficulty.Level0,
                "Wer traut sich am längsten? Steigt aus, bevor es abstürzt!", MinigameKind.Crash)),
            new(OhneZeilen(kat, Abend("Glücksrad"), "Glücksrad", 200, Difficulty.Level0,
                "Das Glücksrad! Wer das Feld aufgedeckt hat, dreht.", MinigameKind.WheelOfFortune)),
            new(MultipleChoice(kat, Abend("Risiko"), "Gepard", 300, Difficulty.Level0,
                "Welches Tier ist an Land am schnellsten?",
                [("Gepard", true), ("Löwe", false), ("Gazelle", false), ("Strauß", false)],
                "Der Gepard – im Sprint über 100 Kilometer pro Stunde."),
                CellSpecialRule.Risk),
            new(MultipleChoice(kat, Abend("Einsatz"), "Merkur", 400, Difficulty.Level0,
                "Welcher Planet ist der Sonne am nächsten?",
                [("Merkur", true), ("Venus", false), ("Mars", false), ("Erde", false)],
                "Der Merkur."),
                CellSpecialRule.Stake, Angekuendigt: true),
        ];

        private static List<AbendZelle> WorteUndMeinungen(Guid kat) =>
        [
            new(Mehrheit(kat, Abend("Mehrheit"), "Mehrheit", 100, Difficulty.Level0,
                "Was gehört unbedingt auf eine Pizza?", variante: 0,
                ["Salami", "Pilze", "Ananas", "Extra Käse"])),
            new(Mehrheit(kat, Abend("Minderheit"), "Minderheit", 200, Difficulty.Level0,
                "Welches Haustier hättet ihr am liebsten? Hier gewinnt die kleinste Gruppe!",
                variante: MehrheitEvaluator.MinorityVariant,
                ["Hund", "Katze", "Papagei", "Schildkröte"])),
            new(MitZeilen(kat, Abend("Erklärbär"), "Erklärbär", 300, Difficulty.Level0,
                "Einer erklärt, alle anderen raten – aber das Wort selbst ist verboten!", MinigameKind.ExplainIt,
                [
                    ("Gartenzwerg", false),
                    ("Lagerfeuer", false),
                    ("Hängematte", false),
                    ("Rolltreppe", false),
                    ("Schneeschaufel", false),
                ])),
            new(MitZeilen(kat, Abend("Bluff"), "Bluff", 400, Difficulty.Level0,
                "Was bedeutet das Wort „Petrichor“? Erfindet eine Antwort, die echt klingt!", MinigameKind.Bluff,
                [
                    ("der Geruch von Regen auf trockener Erde", false),
                    ("Regengeruch", false),
                ])),
        ];

        private static List<AbendZelle> Taktik(Guid kat) =>
        [
            new(MultipleChoice(kat, Abend("Wette"), "Eisbär", 100, Difficulty.Level0,
                "Welche Farbe hat die Haut eines Eisbären unter dem Fell?",
                [("Schwarz", true), ("Weiß", false), ("Rosa", false), ("Grau", false)],
                "Schwarz – so nimmt sie die Wärme der Sonne auf."),
                CellSpecialRule.Bet, Angekuendigt: true),
            new(Standard(kat, Abend("Experte"), "Organ", 200, Difficulty.Level0,
                "Wie heißt das größte Organ des Menschen?",
                ["Man sieht es jeden Tag.", "Es wiegt mehrere Kilogramm."],
                "Die Haut"),
                CellSpecialRule.Expert),
            new(MultipleChoice(kat, Abend("Allianz"), "Gitarre", 300, Difficulty.Level0,
                "Wie viele Saiten hat eine gewöhnliche Gitarre?",
                [("Sechs", true), ("Vier", false), ("Fünf", false), ("Sieben", false)],
                "Sechs."),
                CellSpecialRule.Alliance, Angekuendigt: true),
            new(Standard(kat, Abend("Auktion"), "Fe", 400, Difficulty.Level0,
                "Welches chemische Element hat das Zeichen „Fe“?",
                ["Es ist ein Metall.", "Es rostet."],
                "Eisen"),
                CellSpecialRule.Auction, Angekuendigt: true, Spielweise: AuktionRegel.ZweitpreisSpielweise),
        ];
    }
}
