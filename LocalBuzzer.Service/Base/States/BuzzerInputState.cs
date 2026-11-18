using LocalBuzzer.Service.Hubs.Accessors;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System.Collections.Concurrent;

namespace LocalBuzzer.Service.Base.States
{
    /// <summary>
    /// Das Eingabe-Layout der Schaetzfrage: jeder Spieler tippt einen Wert und bestaetigt ihn.
    /// Die Runde schliesst, sobald alle Mitspieler abgegeben haben.
    /// <para>
    /// Genaues Gegenstueck zu <see cref="BuzzerKeySelector"/>. Die Browser-Seite gab es laengst
    /// (<c>wwwroot/JS/layouts/inputLayout.js</c> ruft <c>SubmitInput</c> auf), serverseitig fehlte
    /// dieser Zustand vollstaendig - <c>ResetLayouts</c> fand kein passendes Layout und liess die
    /// Spieler gesperrt.
    /// </para>
    /// </summary>
    public sealed class BuzzerInputState : IBuzzerLayoutState
    {
        private readonly GameAccessor _gameAccessor;
        private readonly AnswerLock _answerLock;

        public BuzzerInputState(GameAccessor gameAccessor, AnswerLock? answerLock = null)
        {
            _gameAccessor = gameAccessor;
            _answerLock = answerLock ?? new();
        }

        public BuzzerControlsLayout BuzzerControlsLayout => BuzzerControlsLayout.Input;

        public bool Locked { get; set; }

        /// <summary>
        /// Was das Eingabefeld im Browser anbieten soll. Die Feldnamen entsprechen dem, was
        /// <c>inputLayout.js</c> ausliest.
        /// </summary>
        public sealed class BuzzerInputInfo
        {
            /// <summary>HTML-Eingabeart: <c>number</c> oder <c>date</c>.</summary>
            public string InputType { get; set; } = "text";

            /// <summary>Platzhalter im Eingabefeld, nennt ueblicherweise die Einheit.</summary>
            public string Placeholder { get; set; } = "Eingabe";

            /// <summary>Die Frage, zu der eingegeben wird.</summary>
            public Guid? QuestionId { get; set; }
        }

        /// <summary>Der Tipp eines Spielers.</summary>
        public sealed class InputResult
        {
            public Guid PlayerId { get; set; } = Guid.Empty;

            public Player? Player { get; set; }

            /// <summary>Was der Spieler eingetippt hat, unveraendert.</summary>
            public string Value { get; set; } = string.Empty;

            public bool CommittedResult { get; set; }

            /// <summary>Der Rücksetzstand, den das Telefon beim Abgeben sah (P10-4) - <c>resetCount</c> in <c>inputLayout.js</c>.</summary>
            public int? ResetCount { get; set; }
        }

        public BuzzerInputInfo Infos { get; set; } = new();

        public ConcurrentDictionary<Guid, InputResult> InputsForPlayer { get; } = new();

        public object BuzzerStateInfo => new BuzzerInputInfo
        {
            InputType = Infos.InputType,
            Placeholder = Infos.Placeholder,
            QuestionId = Infos.QuestionId,
        };

        /// <summary>
        /// So viele Zeichen nimmt eine Eingabe hoechstens an (M37 E4, Entwurf Abschnitt 4) - der Rest wird abgeschnitten.
        /// Ohne Grenze trug SignalR bis zu 32 KB je Abgabe bis auf den Beamer.
        /// </summary>
        public const int MaxLength = 80;

        /// <summary>
        /// Jeder gibt genau einmal ab (M37 E4, Finale): eine zweite Abgabe wird verworfen, und das Telefon ist danach gesperrt -
        /// auch nach dem Wiederverbinden. Gilt bis zum naechsten Layout; „Runde zuruecksetzen" laesst neu abgeben.
        /// </summary>
        public bool EinmalAbgabe { get; set; }

        /// <summary>Gesperrt fuer alle - oder beim Finale fuer den, der schon abgegeben hat.</summary>
        public bool IsLockedFor(Player? player)
            => Locked || (EinmalAbgabe && player != null && InputsForPlayer.TryGetValue(player.Id, out var eigene) && eigene.CommittedResult);

        /// <summary>
        /// Nimmt den Tipp eines Spielers an. Ein zweiter Tipp desselben Spielers ersetzt den
        /// ersten, solange die Runde offen ist - ausser beim Finale (<see cref="EinmalAbgabe"/>). Gesperrt wird, sobald alle
        /// abgegeben haben. <c>false</c>: verworfen, nichts davon darf den Spielleiter erreichen.
        /// </summary>
        public bool SetInput(InputResult result) => SetInput(result, out _);

        /// <param name="closedRound">Ob dieser Tipp die Runde geschlossen hat - das tut genau einer (M37 P6, R2-4). Zwei letzte
        /// Tipps zugleich lasen danach beide <c>Locked</c>, und die Runde wurde zweimal aufgelöst.</param>
        public bool SetInput(InputResult result, out bool closedRound)
        {
            closedRound = false;

            if (result.Player == null || Locked || !_answerLock.Allows(result.Player.Id))
                return false;

            lock (this)
            {
                if (IsLockedFor(result.Player))
                    return false;

                result.Value = Kuerze(result.Value);

                // Leere Eingaben gelten nicht als Abgabe - sonst schliesst die Runde, sobald
                // jemand versehentlich auf Bestaetigen tippt, und niemand kann mehr nachlegen.
                result.CommittedResult = !string.IsNullOrWhiteSpace(result.Value);

                InputsForPlayer.AddOrUpdate(result.Player.Id, _ => result, (_, _) => result);

                // M35 C3b: nur wer antworten darf, haelt die Runde offen.
                var playersCount = _gameAccessor.GetGame?.Invoke()?.Players?.Count(p => _answerLock.Allows(p.Id)) ?? 0;

                if (playersCount <= 0)
                {
                    Locked = closedRound = true;
                    return true;
                }

                Locked = closedRound = InputsForPlayer.Count == playersCount
                      && InputsForPlayer.All(v => v.Value.CommittedResult);

                return true;
            }
        }

        /// <summary>Auf <see cref="MaxLength"/> Zeichen; ein Zeichen aus zwei Haelften (Emoji) wird nicht zerschnitten.</summary>
        public static string Kuerze(string? wert)
        {
            if (wert == null || wert.Length <= MaxLength)
                return wert ?? string.Empty;

            var laenge = char.IsHighSurrogate(wert[MaxLength - 1]) ? MaxLength - 1 : MaxLength;

            return wert[..laenge];
        }

        /// <summary>In der Sperre (M37 P10, P8-1): sonst landet eine gegen die alte Runde gepruefte Abgabe in der neuen.</summary>
        public void Reset()
        {
            lock (this)
            {
                InputsForPlayer.Clear();
                Locked = false;
            }
        }

        /// <summary>In der Sperre (M37 P10, P8-1): sonst oeffnet eine gerade ausgewertete Abgabe den abgeloesten Zustand wieder.</summary>
        public void ClearAndLock()
        {
            lock (this)
            {
                InputsForPlayer.Clear();
                Infos = new BuzzerInputInfo();
                EinmalAbgabe = false;
                Locked = true;
            }
        }

        /// <summary>Die Spielleitung schließt die Runde - in der Sperre, sonst hebt eine gerade ausgewertete Abgabe das wieder auf (M37 P8, P6-1).</summary>
        public void LockAll()
        {
            lock (this)
            {
                foreach (var item in InputsForPlayer)
                    item.Value.CommittedResult = true;

                Locked = true;
            }
        }
    }
}
