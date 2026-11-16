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

        /// <summary>Nur einige duerfen antworten.</summary>
        public bool Active => antwortende != null;

        /// <summary>Wer antworten darf; <c>null</c>: alle.</summary>
        public IReadOnlyCollection<Guid>? Answerers => antwortende;

        /// <summary>Nur diese duerfen antworten; <c>null</c> hebt die Sperre auf.</summary>
        public void Restrict(IEnumerable<Guid>? erlaubt) => antwortende = erlaubt?.ToHashSet();

        public void Clear() => antwortende = null;

        public bool Allows(Guid playerId) => antwortende is not { } nur || nur.Contains(playerId);
    }
}
