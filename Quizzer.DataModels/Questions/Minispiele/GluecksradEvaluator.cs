namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Die Felder des Gluecksrads (M35 C4). Nicht gespeichert - gewuerfelt am Server, gewertet im Fragefenster.</summary>
    public enum WheelSegment
    {
        /// <summary>Der Dreher bekommt den doppelten Anteil.</summary>
        Double = 1,

        /// <summary>Der Dreher nimmt einem gewaehlten Mitspieler den Anteil ab.</summary>
        Steal = 2,

        /// <summary>Der Dreher schenkt einem gewaehlten Mitspieler den Anteil.</summary>
        Gift = 3,

        /// <summary>Der Dreher verliert den Anteil.</summary>
        BadLuck = 4,

        /// <summary>Der Fuehrende gibt dem Letzten den Anteil.</summary>
        RobinHood = 5,
    }

    /// <summary>Wo das Rad stehen blieb, wer gedreht hat und - bei Klauen und Verschenken - wen es trifft.</summary>
    public sealed record GluecksradWurf(WheelSegment Segment, Guid? Dreher, Guid? Ziel);

    /// <summary>Was das Gluecksrad ergeben hat.</summary>
    /// <param name="Vorschlaege">Je Betroffenem der Vorschlag, schon gekappt (E2).</param>
    /// <param name="Rollen">Je Betroffenem, was ihm geschah - fuer Beamer und Karte dieselben Worte.</param>
    public sealed record GluecksradErgebnis(
        GluecksradWurf Wurf, IReadOnlyDictionary<Guid, Punktevorschlag> Vorschlaege, IReadOnlyDictionary<Guid, string> Rollen)
    {
        public string Describe(Guid spieler) => Rollen.GetValueOrDefault(spieler) ?? "schaut beim Glücksrad zu";
    }

    /// <summary>
    /// Das Gluecksrad (M35 C4): fuenf gleich grosse Felder, gedreht vom Spielleiter fuer den, der das Feld
    /// aufgedeckt hat. Jede Bewegung ist der Anteil B der Feldpunkte (Verdoppeln: 2·B).
    /// <para>
    /// <b>Robin Hood bei Gleichstand:</b> die Fuehrenden geben B zusammen (je B/n), die Letzten bekommen B
    /// zusammen (je B/m) - so wandert, bis auf Rundung, genau B. Stehen alle gleich, passiert nichts. Ein
    /// Minus wird am Kontostand gekappt (E2), ein Plus nie.
    /// </para>
    /// </summary>
    public static class GluecksradEvaluator
    {
        /// <summary>Die Felder in der Reihenfolge am Rad - jedes gleich gross, der Wurf ist der Index.</summary>
        public static IReadOnlyList<WheelSegment> Felder { get; } =
            [WheelSegment.Double, WheelSegment.Steal, WheelSegment.Gift, WheelSegment.BadLuck, WheelSegment.RobinHood];

        /// <summary>
        /// Die Felder im Spiel mit oder ohne Teams - im Teamspiel ohne Robin Hood (M37 T8): Führender und Letzter sind dort
        /// Einzelne, die Rechnung ginge an den Teams vorbei, und beide können im selben Team spielen.
        /// </summary>
        public static IReadOnlyList<WheelSegment> FelderFuer(bool teamspiel) => teamspiel ? FelderImTeamspiel : Felder;

        private static readonly IReadOnlyList<WheelSegment> FelderImTeamspiel = [.. Felder.Where(f => f != WheelSegment.RobinHood)];

        /// <summary>Das Feld zum Wurf in [0, Anzahl); ein Wurf ausserhalb wird an den Rand gelegt.</summary>
        public static WheelSegment Segment(int wurf) => Segment(wurf, false);

        /// <summary>Wie <see cref="Segment(int)"/>, aus den Feldern des Spiels mit oder ohne Teams.</summary>
        public static WheelSegment Segment(int wurf, bool teamspiel)
        {
            var felder = FelderFuer(teamspiel);
            return felder[Math.Clamp(wurf, 0, felder.Count - 1)];
        }

        public static string Name(WheelSegment segment) => segment switch
        {
            WheelSegment.Double => "Verdoppeln",
            WheelSegment.Steal => "Klauen",
            WheelSegment.Gift => "Verschenken",
            WheelSegment.BadLuck => "Pech",
            WheelSegment.RobinHood => "Robin Hood",
            _ => "?",
        };

        /// <summary>Was das Feld bedeutet - steht am Beamer neben dem Namen.</summary>
        public static string Erklaerung(WheelSegment segment) => segment switch
        {
            WheelSegment.Double => "doppelter Anteil für den Dreher",
            WheelSegment.Steal => "der Dreher beklaut jemanden",
            WheelSegment.Gift => "der Dreher beschenkt jemanden",
            WheelSegment.BadLuck => "der Dreher verliert den Anteil",
            WheelSegment.RobinHood => "der Führende gibt dem Letzten",
            _ => string.Empty,
        };

        /// <summary>Klauen und Verschenken brauchen ein Ziel - das waehlt der Dreher am Telefon.</summary>
        public static bool BrauchtZiel(WheelSegment segment) => segment is WheelSegment.Steal or WheelSegment.Gift;

        /// <summary>Die Frage am Telefon des Drehers.</summary>
        public static string Zielfrage(WheelSegment segment)
            => segment == WheelSegment.Steal ? "Wen beklaust du?" : "Wem schenkst du Punkte?";

        /// <summary>Der Ausgang in einem Satz, ohne Punkte - auch fuer das Telefon, das die Kontostaende nicht kennt.</summary>
        public static string Ausgang(GluecksradWurf wurf, Func<Guid, string> name)
        {
            ArgumentNullException.ThrowIfNull(wurf);
            ArgumentNullException.ThrowIfNull(name);

            var dreher = wurf.Dreher is { } d ? name(d) : "Niemand";
            var titel = Name(wurf.Segment);

            if (!BrauchtZiel(wurf.Segment))
            {
                return wurf.Segment switch
                {
                    WheelSegment.Double => $"{titel} – {dreher} bekommt den doppelten Anteil.",
                    WheelSegment.BadLuck => $"{titel} – {dreher} verliert den Anteil.",
                    _ => $"{titel} – der Führende gibt dem Letzten.",
                };
            }

            if (wurf.Ziel is not { } z)
                return $"{titel} – es wurde kein Ziel gewählt.";

            return wurf.Segment == WheelSegment.Steal
                ? $"{titel} – {dreher} beklaut {name(z)}."
                : $"{titel} – {dreher} beschenkt {name(z)}.";
        }

        /// <summary>Die Bewegungen des Wurfs; <paramref name="spieler"/> sind alle Mitspieler - Robin Hood sucht unter ihnen.</summary>
        public static GluecksradErgebnis Werte(
            GluecksradWurf wurf, int anteil, Kontostaende konten, IReadOnlyList<(Guid Id, string Name)> spieler)
        {
            ArgumentNullException.ThrowIfNull(wurf);
            ArgumentNullException.ThrowIfNull(konten);
            ArgumentNullException.ThrowIfNull(spieler);

            var buchung = new Buchung(konten, Name(wurf.Segment));
            string N(Guid id) => spieler.FirstOrDefault(s => s.Id == id).Name ?? "?";

            if (wurf.Segment == WheelSegment.RobinHood)
                RobinHood(buchung, anteil, konten, spieler);
            else if (wurf.Dreher is { } dreher)
                Dreher(buchung, wurf with { Dreher = dreher }, anteil, N);

            return new(wurf, buchung.Vorschlaege, buchung.Rollen);
        }

        private static void Dreher(Buchung buchung, GluecksradWurf wurf, int anteil, Func<Guid, string> name)
        {
            var dreher = wurf.Dreher!.Value;

            switch (wurf.Segment)
            {
                case WheelSegment.Double:
                    buchung.Bewege(dreher, 2 * anteil, "bekommt den doppelten Anteil");
                    break;
                case WheelSegment.BadLuck:
                    buchung.Bewege(dreher, -anteil, "hat Pech");
                    break;
                case WheelSegment.Steal when wurf.Ziel is { } ziel && ziel != dreher:
                    buchung.Bewege(dreher, anteil, $"beklaut {name(ziel)}", klaus: 1);
                    buchung.Bewege(ziel, -anteil, $"von {name(dreher)} beklaut");
                    break;
                case WheelSegment.Gift when wurf.Ziel is { } ziel && ziel != dreher:
                    buchung.Bewege(dreher, -anteil, $"beschenkt {name(ziel)}");
                    buchung.Bewege(ziel, anteil, $"von {name(dreher)} beschenkt");
                    break;
            }
        }

        private static void RobinHood(Buchung buchung, int anteil, Kontostaende konten, IReadOnlyList<(Guid Id, string Name)> spieler)
        {
            if (spieler.Count < 2)
                return;

            var oben = spieler.Max(s => konten.Von(s.Id));
            var unten = spieler.Min(s => konten.Von(s.Id));

            if (oben == unten)
                return;

            var fuehrende = spieler.Where(s => konten.Von(s.Id) == oben).ToList();
            var letzte = spieler.Where(s => konten.Von(s.Id) == unten).ToList();

            foreach (var f in fuehrende)
                buchung.Bewege(f.Id, -Punkteanteil.Von(anteil, 100 / fuehrende.Count), "gibt als Führender ab");

            foreach (var l in letzte)
                buchung.Bewege(l.Id, Punkteanteil.Von(anteil, 100 / letzte.Count), "bekommt als Letzter");
        }

        /// <summary>Sammelt je Spieler Vorschlag und Rolle; ein Minus wird einmal gekappt (E2).</summary>
        private sealed class Buchung(Kontostaende konten, string feld)
        {
            public Dictionary<Guid, Punktevorschlag> Vorschlaege { get; } = new();

            public Dictionary<Guid, string> Rollen { get; } = new();

            // klaus: nur beim Klauen 1 (M35 C9) - Robin Hood nimmt, aber kein Spieler klaut.
            public void Bewege(Guid spieler, int punkte, string rolle, int klaus = 0)
            {
                var gekappt = konten.Kappe(spieler, punkte);

                Rollen[spieler] = rolle;
                Vorschlaege[spieler] = new(gekappt, $"Glücksrad ({feld}): {rolle} {Punkteanteil.Text(gekappt)}"
                                                    + (gekappt != punkte ? Punkteanteil.GekapptText : string.Empty), klaus);
            }
        }
    }
}
