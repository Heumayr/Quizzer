using Quizzer.DataModels.Transfer.Sync;

namespace Quizzer.Client
{
    /// <summary>
    /// Was die Massenlösch-Sperre aufgehalten hat (V06): wie viele Fragen und Spiele am Server gelöscht würden, und
    /// genau welche. Eine Freigabe gilt nur für diese Schlüssel - kommt bis zum Senden eine weitere Löschung dazu, greift
    /// die Sperre wieder.
    /// </summary>
    public sealed class MassDeletion
    {
        private readonly HashSet<SyncKey> keys;

        public MassDeletion(int questions, int games, int known, IEnumerable<SyncKey> keys)
        {
            Questions = questions;
            Games = games;
            Known = known;
            this.keys = keys.ToHashSet();
        }

        public int Questions { get; }

        public int Games { get; }

        /// <summary>Wie viele Fragen und Spiele das Konto am Server kennt.</summary>
        public int Known { get; }

        public IReadOnlyCollection<SyncKey> Keys => keys;

        /// <summary>Gilt die Freigabe für diese Löschungen - genau diese, keine mehr und keine andere?</summary>
        public bool Covers(IEnumerable<SyncKey> deletes) => keys.SetEquals(deletes);

        /// <summary>„12 Fragen und 2 Spiele" - für Statuszeile und Rückfrage.</summary>
        public string Description
        {
            get
            {
                var teile = new List<string>();

                if (Questions > 0)
                    teile.Add(Questions == 1 ? "1 Frage" : $"{Questions} Fragen");

                if (Games > 0)
                    teile.Add(Games == 1 ? "1 Spiel" : $"{Games} Spiele");

                return string.Join(" und ", teile);
            }
        }
    }
}
