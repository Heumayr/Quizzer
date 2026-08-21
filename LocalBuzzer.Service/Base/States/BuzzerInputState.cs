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

        public BuzzerInputState(GameAccessor gameAccessor)
        {
            _gameAccessor = gameAccessor;
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
        /// Nimmt den Tipp eines Spielers an. Ein zweiter Tipp desselben Spielers ersetzt den
        /// ersten, solange die Runde offen ist. Gesperrt wird, sobald alle abgegeben haben.
        /// </summary>
        public void SetInput(InputResult result)
        {
            if (result.Player == null || Locked)
                return;

            lock (this)
            {
                // Leere Eingaben gelten nicht als Abgabe - sonst schliesst die Runde, sobald
                // jemand versehentlich auf Bestaetigen tippt, und niemand kann mehr nachlegen.
                result.CommittedResult = !string.IsNullOrWhiteSpace(result.Value);

                InputsForPlayer.AddOrUpdate(result.Player.Id, _ => result, (_, _) => result);

                var playersCount = _gameAccessor.GetGame?.Invoke()?.Players?.Count() ?? 0;

                if (playersCount <= 0)
                {
                    Locked = true;
                    return;
                }

                Locked = InputsForPlayer.Count == playersCount
                      && InputsForPlayer.All(v => v.Value.CommittedResult);
            }
        }

        public void Reset()
        {
            InputsForPlayer.Clear();
            Locked = false;
        }

        public void ClearAndLock()
        {
            InputsForPlayer.Clear();
            Infos = new BuzzerInputInfo();
            Locked = true;
        }

        public void LockAll()
        {
            foreach (var item in InputsForPlayer)
                item.Value.CommittedResult = true;

            Locked = true;
        }
    }
}
