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

        public List<string> Confirmations { get; } = new();

        public List<string> Informations { get; } = new();

        public bool Confirm(string message, string caption)
        {
            Confirmations.Add(caption);
            return answer;
        }

        public void Inform(string message, string caption = "") => Informations.Add(message);
    }
}
