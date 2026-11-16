namespace LocalBuzzer.Service.Base
{
    /// <summary>
    /// Wer die gewoehnliche Frage beantworten darf (M35 C3b): Auktion, Experte und Allianz lassen nur einige.
    /// Gilt fuer Buzzer, Tastenwahl und Eingabe und ueber jedes Zuruecksetzen der Runde hinweg, bis die Frage zu
    /// ist. Eine Abgabe eines Gesperrten verwirft der Zustand selbst - sie kommt aus dem WLAN.
    /// <para>Gesetzt vom Oberflaechen-Thread, gelesen von den Hub-Threads: die Menge wird nur ersetzt, nie geaendert.</para>
    /// </summary>
    public sealed class AnswerLock
    {
        private volatile HashSet<Guid>? antwortende;
        private volatile IReadOnlyDictionary<Guid, string>? gruende;

        /// <summary>Nur einige duerfen antworten.</summary>
        public bool Active => antwortende != null;

        /// <summary>Wer antworten darf; <c>null</c>: alle.</summary>
        public IReadOnlyCollection<Guid>? Answerers => antwortende;

        /// <summary>
        /// Nur diese duerfen antworten; <c>null</c> hebt die Sperre auf. <paramref name="saetze"/>: je Gesperrtem der Satz
        /// fuer sein Telefon, etwa wer ihn per Joker gesperrt hat (M35 C7b); ohne Satz sagt das Telefon es allgemein.
        /// </summary>
        public void Restrict(IEnumerable<Guid>? erlaubt, IReadOnlyDictionary<Guid, string>? saetze = null)
        {
            gruende = erlaubt == null ? null : saetze?.ToDictionary();
            antwortende = erlaubt?.ToHashSet();
        }

        public void Clear()
        {
            antwortende = null;
            gruende = null;
        }

        /// <summary>Der Satz fuer das Telefon eines Gesperrten; <c>null</c>: keiner.</summary>
        public string? ReasonFor(Guid playerId) => gruende?.GetValueOrDefault(playerId);

        public bool Allows(Guid playerId) => antwortende is not { } nur || nur.Contains(playerId);
    }
}
