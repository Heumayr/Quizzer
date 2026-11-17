namespace Quizzer.DataModels.Models.Buzzer
{
    /// <summary>
    /// Die Team-Abstimmung an diesem Telefon (M37 T7, F-M37-13), im Feld <c>team.vote</c>: vor dem Schluss nur die eigene Stimme
    /// und wie viele des eigenen Teams abgestimmt haben, danach die Antwort des eigenen Teams. Fremde Teams stehen nie darin -
    /// alle Teams zeigt nur der Beamer.
    /// </summary>
    public sealed class TeamVoteClientInfo
    {
        /// <summary>Die eigene Stimme als Tasten; leer, solange keine abgegeben ist.</summary>
        public List<string> Own { get; set; } = new();

        /// <summary>Wie viele des eigenen Teams abgestimmt haben.</summary>
        public int Voted { get; set; }

        /// <summary>Wie viele des eigenen Teams abstimmen dürfen.</summary>
        public int Of { get; set; }

        /// <summary>Ob die Abstimmung zu ist - alle haben abgestimmt oder die Spielleitung hat geschlossen.</summary>
        public bool Closed { get; set; }

        /// <summary>Nach dem Schluss die Antwort des eigenen Teams, etwa „B (2 von 3)"; leer, solange offen oder noch nicht ausgewertet.</summary>
        public string Result { get; set; } = string.Empty;
    }
}
