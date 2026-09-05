using Quizzer.Base;

namespace Quizzer.UnitTests
{
    /// <summary>
    /// Beantwortet Rueckfragen nach Vorgabe und schreibt mit, was gefragt wurde.
    /// Ersetzt die Meldungsfenster, die einen Testlauf sonst anhalten wuerden.
    /// </summary>
    public sealed class RecordingUserPrompt : IUserPrompt
    {
        private readonly bool answer;

        public RecordingUserPrompt(bool answer = true) => this.answer = answer;

        /// <summary>Was der Nutzer liest: Text und Ueberschrift zusammen.</summary>
        /// <param name="Message">Der Text im Fenster.</param>
        /// <param name="Caption">Die Ueberschrift des Fensters.</param>
        public sealed record Prompt(string Message, string Caption);

        /// <summary>
        /// Die Ueberschriften der Rueckfragen - die bestehenden Zusicherungen lesen sie so.
        /// </summary>
        public List<string> Confirmations { get; } = new();

        /// <summary>Die Texte der Hinweise.</summary>
        public List<string> Informations { get; } = new();

        /// <summary>
        /// Rueckfragen mit Text <b>und</b> Ueberschrift.
        /// <para>
        /// Bis 2026-09-06 hielt der Rekorder von einer Rueckfrage nur die Ueberschrift fest und
        /// von einem Hinweis nur den Text. Ein Test konnte damit nicht pruefen, was der Nutzer
        /// wirklich liest.
        /// </para>
        /// </summary>
        public List<Prompt> Confirms { get; } = new();

        /// <summary>Hinweise mit Text und Ueberschrift.</summary>
        public List<Prompt> Informs { get; } = new();

        public bool Confirm(string message, string caption)
        {
            Confirmations.Add(caption);
            Confirms.Add(new Prompt(message, caption));

            return answer;
        }

        public void Inform(string message, string caption = "")
        {
            Informations.Add(message);
            Informs.Add(new Prompt(message, caption));
        }
    }
}
