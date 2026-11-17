using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models;

namespace Quizzer.DataModels.Questions.Sonderregeln
{
    /// <summary>Eine Spielweise einer Sonderregel (M35 C3b), etwa „Zweitpreis" bei der Auktion.</summary>
    /// <param name="Name">Im Auswahldialog.</param>
    /// <param name="RulesText">Der Regeltext dieser Spielweise; <see cref="SpecialRuleProfiles.SharePlaceholder"/> wird ersetzt.</param>
    public sealed record SpecialRuleVariant(string Name, string RulesText);

    /// <summary>Ob eine Regel im Teamspiel gespielt wird (M37 T5, Entwurf Abschnitt 7).</summary>
    public enum TeamspielEignung
    {
        /// <summary>Mit und ohne Teams - Einsatz und Risiko wirken je Person.</summary>
        Erlaubt = 0,

        /// <summary>Nur im Teamspiel - eine Teamregel wie die Teamberatung.</summary>
        Nur = 1,

        /// <summary>Nie im Teamspiel - Auktion, Wette, Experte, Allianz und Kopfgeld rechnen Spieler gegeneinander.</summary>
        Nie = 2,
    }

    /// <summary>Was eine Sonderregel ausmacht - Name und Regeltext fuer Beamer, Telefon und Spielaufbau.</summary>
    /// <param name="Rule">Der Wert an der Zelle.</param>
    /// <param name="DisplayName">„Einsatz" - im Spielaufbau, auf der Marke und am Beamer.</param>
    /// <param name="RulesText">Der Regeltext, den Beamer und Telefon in der Vorphase zeigen - ohne Spielweisen.</param>
    public sealed record SpecialRuleProfile(CellSpecialRule Rule, string DisplayName, string RulesText)
    {
        /// <summary>Anteil in Prozent der Feldpunkte, wenn die Zelle keinen eigenen traegt; 0: die Regel kennt keinen.</summary>
        public int DefaultShare { get; init; }

        /// <summary>Ob der Spielaufbau ein Feld „Anteil %" zeigt.</summary>
        public bool ShowShare => DefaultShare > 0;

        /// <summary>Die Spielweisen, Index = <see cref="GameGridCoordinate.SpecialRuleVariant"/>; leer: nur eine.</summary>
        public IReadOnlyList<SpecialRuleVariant> Variants { get; init; } = [];

        /// <summary>Ob der Spielaufbau eine Spielweise anbietet.</summary>
        public bool ShowVariant => Variants.Count > 1;

        /// <summary>Ob die Regel eine Vorphase am Telefon spielt - das Kopfgeld nicht, es steht beim Oeffnen fest.</summary>
        public bool HasPrePhase { get; init; } = true;

        /// <summary>Ob die Regel im Teamspiel gespielt wird (M37 T5).</summary>
        public TeamspielEignung Teamspiel { get; init; } = TeamspielEignung.Erlaubt;

        /// <summary>Eine Teamregel: sie gibt es nur im Teamspiel.</summary>
        public bool IstTeamregel => Teamspiel == TeamspielEignung.Nur;

        /// <summary>Ob die Regel in einem Spiel mit oder ohne Teams gespielt wird; eine unbekannte Eignung nie (fail-closed).</summary>
        public bool PasstZu(bool teamspiel) => Teamspiel switch
        {
            TeamspielEignung.Erlaubt => true,
            TeamspielEignung.Nur => teamspiel,
            TeamspielEignung.Nie => !teamspiel,
            _ => false,
        };

        /// <summary>Der Regeltext der Spielweise mit eingesetztem Anteil; eine unbekannte Spielweise liest sich wie die erste.</summary>
        public string RulesTextFor(int variant, int share)
        {
            var text = Variants.Count == 0 ? RulesText
                : variant >= 0 && variant < Variants.Count ? Variants[variant].RulesText
                : Variants[0].RulesText;

            return text.Replace(SpecialRuleProfiles.SharePlaceholder, share.ToString());
        }
    }

    /// <summary>
    /// Die Sonderregeln (M35 C3) - die einzige Quelle fuer Name, Regeltext, Anteil und Spielweisen. Eine neue
    /// Regel bringt hier eine Zeile, am Server ihre Vorphase und im Fragefenster ihre Wertung.
    /// </summary>
    public static class SpecialRuleProfiles
    {
        /// <summary>Was die Marke vor den Namen setzt - nie allein Farbe.</summary>
        public const string Symbol = "⚑";

        /// <summary>Steht im Regeltext fuer den Anteil der Zelle - <see cref="SpecialRuleProfile.RulesTextFor"/> setzt die Zahl ein.</summary>
        public const string SharePlaceholder = "{Anteil}";

        /// <summary>Ein eigener Anteil an der Zelle: 1 bis 100 Prozent; 0 heisst Vorgabe.</summary>
        public const int MaxShare = 100;

        public static IReadOnlyList<SpecialRuleProfile> All { get; } =
        [
            new(CellSpecialRule.Stake, "Einsatz",
                "Vor der Frage setzt jeder am Telefon Punkte – höchstens seinen Kontostand oder die Feldpunkte, "
                + "je nachdem, was mehr ist. Richtig gewinnt den Einsatz, falsch verliert ihn. "
                + "Wer nichts setzt, spielt um die Feldpunkte."),
            new(CellSpecialRule.Risk, "Risiko",
                "Vor der Frage entscheidet jeder am Telefon: doppelt spielen? Wer ja wählt, bekommt Plus und "
                + "Minus doppelt. Wer nein wählt oder nichts, spielt wie immer."),
            new(CellSpecialRule.Auction, "Auktion", AuktionText("zahlt sein Gebot"))
            {
                // Der Index ist GameGridCoordinate.SpecialRuleVariant - AuktionRegel.ZweitpreisSpielweise zeigt auf den zweiten.
                Variants =
                [
                    new("Höchstpreis", AuktionText("zahlt sein Gebot")),
                    new("Zweitpreis", AuktionText("zahlt nur das zweithöchste Gebot (allein geboten: nichts)")),
                ],
                Teamspiel = TeamspielEignung.Nie,
            },
            new(CellSpecialRule.Bet, "Wette",
                "Vor der Frage wettet jeder am Telefon auf einen Mitspieler – auch auf sich selbst. Einsatz: "
                + $"{SharePlaceholder} % der Feldpunkte. Antwortet der Gewählte richtig, gewinnt die Wette, und zwar umso "
                + "mehr, je weiter hinten er liegt (der Letzte bringt das Doppelte); sonst ist der Einsatz weg. "
                + "Die eigene Antwort zählt wie immer dazu.")
            {
                DefaultShare = 50,
                Teamspiel = TeamspielEignung.Nie,
            },
            new(CellSpecialRule.Expert, "Experte",
                "Vor der Frage wählt jeder am Telefon, wer für alle antworten soll – nicht sich selbst. Der "
                + "Meistgewählte antwortet allein (bei Gleichstand entscheidet der Spielleiter). Ist er richtig, bekommt "
                + $"er die Feldpunkte und jeder, der ihn gewählt hat, {SharePlaceholder} % der Feldpunkte dazu. Wählt "
                + "niemand, spielen alle wie immer.")
            {
                DefaultShare = 25,
                Teamspiel = TeamspielEignung.Nie,
            },
            new(CellSpecialRule.Alliance, "Allianz",
                "Wer das Feld aufgedeckt hat, wählt am Telefon einen Partner. Nur die beiden antworten. Sind beide "
                + "richtig, bekommen beide die vollen Punkte – sonst gibt es für beide kein Plus, ein Minus bleibt. Hat "
                + "niemand das Feld aufgedeckt, gibt es keine Allianz.")
            {
                Teamspiel = TeamspielEignung.Nie,
            },
            new(CellSpecialRule.Bounty, "Kopfgeld",
                "Auf den Führenden ist ein Kopfgeld ausgesetzt – er wird am Beamer genannt. Antwortet ein anderer "
                + $"richtig und der Führende nicht, nimmt er ihm {SharePlaceholder} % der Feldpunkte ab, jeder Jäger "
                + "für sich. Sind alle gleichauf, entfällt das Kopfgeld.")
            {
                DefaultShare = 50,
                HasPrePhase = false,
                Teamspiel = TeamspielEignung.Nie,
            },
            new(CellSpecialRule.TeamCouncil, "Teamberatung",
                "Jedes Team berät sich, dann gibt sein Sprecher am Telefon die Antwort für alle – die anderen sehen, mit "
                + "wem sie beraten. Richtig bringt dem Team die Feldpunkte, falsch kostet es die Minuspunkte, beides gleich "
                + "auf die Mitglieder verteilt. Nach der Frage spricht in jedem Team der Nächste.")
            {
                HasPrePhase = false,
                Teamspiel = TeamspielEignung.Nur,
            },
        ];

        private static string AuktionText(string preis)
            => "Vor der Frage bietet jeder geheim am Telefon – höchstens seinen Kontostand oder die Feldpunkte, "
               + $"0 heißt passen. Das höchste Gebot gewinnt (bei Gleichstand das frühere) und {preis}. Nur der "
               + "Gewinner antwortet: richtig bringt die doppelten Feldpunkte, falsch kostet nur den Preis. "
               + "Bietet niemand, spielen alle wie immer.";

        public static bool TryFor(CellSpecialRule rule, out SpecialRuleProfile profile)
        {
            profile = All.FirstOrDefault(p => p.Rule == rule)!;
            return profile != null;
        }

        /// <summary>Der Name der Regel; „Keine" ohne Regel, die Zahl bei einer unbekannten.</summary>
        public static string NameOf(CellSpecialRule rule)
            => rule == CellSpecialRule.None ? "Keine"
                : TryFor(rule, out var profil) ? profil.DisplayName
                : $"Unbekannt ({(int)rule})";

        /// <summary>Der Anteil, mit dem die Zelle spielt: ihr eigener, sonst die Vorgabe der Regel; 0 bei keiner.</summary>
        public static int ShareOf(GameGridCoordinate zelle)
            => zelle.SpecialRuleShare > 0 ? zelle.SpecialRuleShare
                : TryFor(zelle.SpecialRule, out var profil) ? profil.DefaultShare
                : 0;

        /// <summary>Der Regeltext der Zelle - Spielweise und Anteil eingesetzt; leer ohne bekannte Regel.</summary>
        public static string RulesTextOf(GameGridCoordinate zelle)
            => TryFor(zelle.SpecialRule, out var profil) ? profil.RulesTextFor(zelle.SpecialRuleVariant, ShareOf(zelle)) : string.Empty;

        /// <summary>
        /// Ob die Regel dieser Zelle gespielt wird: bekannt, die Frage ist eine gewoehnliche, und die Regel passt zum Spiel
        /// mit oder ohne Teams (M37 T5). Ein Minispiel spielt nie mit Sonderregel - fail-closed auch, wenn der Typ nicht
        /// geladen ist.
        /// </summary>
        public static bool Applies(GameGridCoordinate zelle, QuestionBase? frage, bool teamspiel)
            => zelle.SpecialRule != CellSpecialRule.None
               && TryFor(zelle.SpecialRule, out var profil)
               && profil.PasstZu(teamspiel)
               && frage != null
               && frage.Typ != QuestionType.Minigame;

        /// <summary>Was Spielaufbau und Verteilen anbieten: nur Regeln, die zu diesem Spiel passen (M37 T5).</summary>
        public static IReadOnlyList<SpecialRuleProfile> Angeboten(bool teamspiel) => All.Where(p => p.PasstZu(teamspiel)).ToList();
    }
}
