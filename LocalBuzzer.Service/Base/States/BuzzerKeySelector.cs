using LocalBuzzer.Service.Hubs.Accessors;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System.Collections.Concurrent;

namespace LocalBuzzer.Service.Base.States
{
    public sealed class BuzzerKeySelector : IBuzzerLayoutState
    {
        private readonly GameAccessor _gameAccessor;

        public BuzzerKeySelector(GameAccessor gameAccessor)
        {
            _gameAccessor = gameAccessor;
        }

        public BuzzerControlsLayout BuzzerControlsLayout => BuzzerControlsLayout.KeySelect;

        public bool Locked { get; set; }

        public sealed class BuzzerKeySelectorInfo
        {
            public bool ShowDesignations { get; set; } = true;
            public Dictionary<string, string> KeysAndDesignations { get; set; } = new();
            public int MaxAllowedSelections { get; set; } = 1;
            public Guid? QuestionId { get; set; } = null;

            /// <summary>
            /// Reihenfolgefrage (M12): die Reihenfolge der Klicks ist die Antwort, und abgegeben
            /// wird erst, wenn <b>alle</b> Tasten gewählt sind. Das Telefon zeigt dann den Platz
            /// auf jeder gewählten Taste statt eines Hakens.
            /// </summary>
            public bool Ordered { get; set; }

            /// <summary>
            /// Zuordnung (M12): die festen Begriffe in ihrer Folge. Das Telefon zeigt damit an,
            /// zu welchem Begriff gerade gewählt wird. Leer bei allen anderen Typen.
            /// </summary>
            public List<string> Prompts { get; set; } = new();

            /// <summary>
            /// Wahr oder falsch (M12): jede Taste ist eine Aussage, das Telefon zeigt je Aussage
            /// „wahr" und „falsch". Abgegeben werden die Tasten der als wahr beurteilten - auch
            /// keine.
            /// </summary>
            public bool TrueFalse { get; set; }
        }

        public sealed class SelectionResult
        {
            public Guid PlayerId { get; set; } = Guid.Empty;
            public Player? Player { get; set; }
            public List<string> SelectedKeys { get; set; } = new();
            public bool CommittedResult { get; set; }
        }

        public BuzzerKeySelectorInfo Infos { get; set; } = new();

        public ConcurrentDictionary<Guid, SelectionResult> KeyResultsForPlayer { get; } = new();

        /// <summary>
        /// Was das Telefon ueber die Tastenwahl erfahren muss. Alles kommt aus <see cref="Infos"/> -
        /// hier stand bis 2026-09-05 ein zweites, nie gesetztes Feld fuer die erlaubte Anzahl, und
        /// damit erreichte die Einstellung des Spielleiters das Telefon nie.
        /// </summary>
        public object BuzzerStateInfo => new BuzzerKeySelectorInfo
        {
            ShowDesignations = Infos.ShowDesignations,
            KeysAndDesignations = Infos.KeysAndDesignations,
            MaxAllowedSelections = Infos.MaxAllowedSelections,
            QuestionId = Infos.QuestionId,
            Ordered = Infos.Ordered,
            Prompts = Infos.Prompts,
            TrueFalse = Infos.TrueFalse
        };

        public void SetSelectedKeys(SelectionResult results)
        {
            if (results.Player == null || Locked)
                return;

            lock (this)
            {
                if (results.SelectedKeys.Count > Infos.MaxAllowedSelections || !IsCompleteOrder(results.SelectedKeys))
                {
                    results.SelectedKeys.Clear();
                    results.CommittedResult = false;
                }
                else
                {
                    results.CommittedResult = true;
                }

                KeyResultsForPlayer.AddOrUpdate(results.Player.Id, _ => results, (_, _) => results);

                var playersCount = _gameAccessor.GetGame?.Invoke()?.Players?.Count() ?? 0;

                if (playersCount <= 0)
                {
                    Locked = true;
                    return;
                }

                Locked = KeyResultsForPlayer.Count == playersCount
                      && KeyResultsForPlayer.All(v => v.Value.CommittedResult);
            }
        }

        /// <summary>
        /// Ob eine Abgabe zu einer Reihenfolgefrage taugt: jede Taste genau einmal, alle gewählt,
        /// keine unbekannte. Ohne Reihenfolge ist jede Abgabe recht.
        /// <para>
        /// <b>Hier und nicht nur am Telefon geprüft:</b> die Seite kommt aus dem WLAN des
        /// Spielorts, und eine doppelte Taste bekäme sonst zwei Plätze gutgeschrieben.
        /// </para>
        /// </summary>
        public bool IsCompleteOrder(IReadOnlyCollection<string> keys)
        {
            // Wahr oder falsch: jede Taste hoechstens einmal und nur bekannte - sonst zaehlte eine
            // doppelt geschickte Aussage zweimal.
            if (Infos.TrueFalse)
                return keys.Distinct(StringComparer.Ordinal).Count() == keys.Count
                    && keys.All(k => k != null && Infos.KeysAndDesignations.ContainsKey(k));

            if (!Infos.Ordered)
                return true;

            return keys.Count == Infos.MaxAllowedSelections
                && keys.Distinct(StringComparer.Ordinal).Count() == keys.Count
                && keys.All(k => k != null && Infos.KeysAndDesignations.ContainsKey(k));
        }

        public void Reset()
        {
            KeyResultsForPlayer.Clear();
            Locked = false;
        }

        public void ClearAndLock()
        {
            Infos.KeysAndDesignations.Clear();
            KeyResultsForPlayer.Clear();
            Infos.ShowDesignations = true;
            Infos.Ordered = false;
            Infos.Prompts = new();
            Infos.TrueFalse = false;
            Locked = true;
        }

        public void LockAll()
        {
            foreach (var item in KeyResultsForPlayer)
                item.Value.CommittedResult = true;

            Locked = true;
        }
    }
}