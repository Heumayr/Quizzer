namespace Quizzer.DataModels.Models.Buzzer
{
    /// <summary>
    /// Das Team dieses Telefons (M37 T4), im Feld <c>team</c> von <see cref="ClientLayoutStateDto"/>. Name, Symbol und Platz
    /// kommen aus der festen Palette <c>TeamFarben</c> - das Telefon setzt nur die Klasse <c>team-farbe-N</c>, nie einen
    /// Farbwert, und zeigt die Farbe nie allein.
    /// </summary>
    public sealed class TeamClientInfo
    {
        /// <summary>Der Teamname, am Telefon nur als Text gesetzt.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Das Symbol der Teamfarbe (●▲■…).</summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>Der Platz in der Palette, 0 bis 7; -1, wenn das Team keinen gültigen trägt.</summary>
        public int ColorIndex { get; set; }

        /// <summary>Wer für das Team spricht (M37 T5), als Anzeigename - am Telefon „Sprecher: Anna“, nur als Text; leer ohne.</summary>
        public string Speaker { get; set; } = string.Empty;

        /// <summary>Die Team-Abstimmung dieses Telefons (M37 T7); <c>null</c>, solange keine läuft.</summary>
        public TeamVoteClientInfo? Vote { get; set; }
    }
}
