namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Eine gespielte Runde des Erklaerbaers - je Runde ein Begriff.</summary>
    /// <param name="Erklaerer">Wer erklaert hat.</param>
    /// <param name="Rater">Wer den Begriff zuerst erraten hat (Serveruhr beim Eintreffen); <c>null</c>, wenn niemand.</param>
    /// <param name="Verraten">Der Spielleiter hat gewertet, dass der Erklaerer das Wort gesagt hat.</param>
    public sealed record ErklaerbaerRunde(Guid Erklaerer, Guid? Rater, bool Verraten = false)
    {
        /// <summary>Ob die Runde Punkte bringt: erraten und nicht verraten.</summary>
        public bool Zaehlt => Rater != null && !Verraten;
    }

    /// <summary>Ein angenommener Rateversuch, gemeldet an den Spielleiter.</summary>
    /// <param name="Richtig">Ob er den Begriff getroffen hat - dann ist die Runde zu.</param>
    /// <param name="Erklaerer">Wer in dieser Runde erklaert.</param>
    public sealed record ErklaerbaerVersuch(bool Richtig, Guid Erklaerer);

    /// <summary>Was der Erklaerbaer ueber alle gespielten Runden ergeben hat.</summary>
    /// <param name="Runden">Die gespielten Runden.</param>
    /// <param name="Topf">Was eine erratene Runde dem Rater und dem Erklaerer je bringt.</param>
    /// <param name="Vorschlaege">Die Punkte je Spieler mit Grund (E1).</param>
    public sealed record ErklaerbaerErgebnis(
        IReadOnlyList<ErklaerbaerRunde> Runden, int Topf, IReadOnlyDictionary<Guid, Punktevorschlag> Vorschlaege)
    {
        public int PunkteVon(Guid spieler) => Vorschlaege.GetValueOrDefault(spieler)?.Punkte ?? 0;

        public int ErratenVon(Guid spieler) => Runden.Count(r => r.Zaehlt && r.Rater == spieler);

        public int ErklaertVon(Guid spieler) => Runden.Count(r => r.Zaehlt && r.Erklaerer == spieler);

        /// <summary>Die meisten Punkte; leer, wenn keiner welche hat.</summary>
        public IReadOnlySet<Guid> Gewinner
        {
            get
            {
                var beste = Vorschlaege.Values.Select(v => v.Punkte).DefaultIfEmpty(0).Max();
                return beste > 0 ? Vorschlaege.Where(v => v.Value.Punkte == beste).Select(v => v.Key).ToHashSet() : new HashSet<Guid>();
            }
        }
    }

    /// <summary>
    /// Der Erklaerbaer (M35 C6): je Runde erklaert einer einen Begriff, die anderen raten am Telefon. Wer zuerst
    /// richtig liegt, und der Erklaerer bekommen je den Topf <c>Feldpunkte / Runden</c>, kaufmaennisch gerundet - wer
    /// alles erklaert, kommt so auf die Feldpunkte. Eine verratene Runde bringt niemandem etwas, auch dem Rater nicht
    /// (Annahme C6a: wer das gesagte Wort eintippt, hat nichts erraten). Ein Minus gibt es nicht.
    /// </summary>
    public static class ErklaerbaerEvaluator
    {
        /// <summary>
        /// Plausibilitaetsgrenze am Server, je Spieler - wie beim Buchstabensalat: ein Mensch tippt kaum mehr als
        /// drei Woerter in fuenf Sekunden; ein Skript mit einer Wortliste bliebe darunter chancenlos.
        /// </summary>
        public const int MaxVersucheJeFenster = BuchstabensalatEvaluator.MaxVersucheJeFenster;

        /// <summary>Das Fenster zu <see cref="MaxVersucheJeFenster"/>.</summary>
        public const int FensterSekunden = BuchstabensalatEvaluator.FensterSekunden;

        /// <summary>Was eine erratene Runde dem Rater und dem Erklaerer je bringt; ohne Runden 0.</summary>
        public static int Topf(int feldpunkte, int runden)
            => runden <= 0 ? 0 : (int)Math.Round((double)feldpunkte / runden, MidpointRounding.AwayFromZero);

        /// <summary>Die Wertung ueber alle gespielten Runden: je Spieler die Summe als Vorschlag mit Grund.</summary>
        /// <param name="runden">Die gespielten Runden.</param>
        /// <param name="gesamtRunden">So viele Runden hat das Minispiel - der Topf haengt daran, nicht an den gespielten.</param>
        public static ErklaerbaerErgebnis Werte(
            IReadOnlyList<ErklaerbaerRunde> runden, int gesamtRunden, int feldpunkte, Kontostaende konten)
        {
            ArgumentNullException.ThrowIfNull(runden);
            ArgumentNullException.ThrowIfNull(konten);

            var topf = Topf(feldpunkte, gesamtRunden);
            var roh = new ErklaerbaerErgebnis(runden, topf, new Dictionary<Guid, Punktevorschlag>());
            var vorschlaege = new Dictionary<Guid, Punktevorschlag>();

            foreach (var id in runden.Where(r => r.Zaehlt).SelectMany(r => new[] { r.Rater!.Value, r.Erklaerer }).Distinct())
            {
                var punkte = konten.Kappe(id, topf * (roh.ErratenVon(id) + roh.ErklaertVon(id)));

                if (punkte != 0)
                    vorschlaege[id] = new(punkte, $"Erklärbär: {Describe(roh, id)} {Punkteanteil.Text(punkte)}");
            }

            return roh with { Vorschlaege = vorschlaege };
        }

        /// <summary>„1× erraten, 2× erklärt" - fuer Beamer, Spielerkarte und Grund dieselben Worte.</summary>
        public static string Describe(ErklaerbaerErgebnis ergebnis, Guid spieler)
        {
            ArgumentNullException.ThrowIfNull(ergebnis);

            return $"{ergebnis.ErratenVon(spieler)}× erraten, {ergebnis.ErklaertVon(spieler)}× erklärt";
        }

        /// <summary>Der Satz auf der Spielerkarte.</summary>
        public static string Karte(ErklaerbaerErgebnis ergebnis, Guid spieler)
            => $"{Describe(ergebnis, spieler)} – {Punkteanteil.Text(ergebnis.PunkteVon(spieler))}";

        /// <summary>Was eine Runde am Beamer ergeben hat - der Begriff ist dann kein Geheimnis mehr.</summary>
        public static string Ausgang(ErklaerbaerRunde runde, string begriff, Func<Guid, string> name)
        {
            ArgumentNullException.ThrowIfNull(runde);
            ArgumentNullException.ThrowIfNull(name);

            return runde.Verraten ? $"„{begriff}“ – verraten, zählt nicht"
                : runde.Rater is { } rater ? $"{name(rater)} hat „{begriff}“ erraten – erklärt von {name(runde.Erklaerer)}"
                : $"Niemand hat „{begriff}“ erraten";
        }
    }
}
