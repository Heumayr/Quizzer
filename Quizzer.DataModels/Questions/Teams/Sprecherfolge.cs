namespace Quizzer.DataModels.Questions.Teams
{
    /// <summary>Ein Mitglied, wie die Sprecherfolge es braucht: Kennung und Anzeigename.</summary>
    public sealed record Teammitglied(Guid Id, string Name);

    /// <summary>
    /// Wer im Team spricht (M37 T5, Entwurf Abschnitt 3): die Mitglieder nach Anzeigename, bei gleichem Namen nach Kennung,
    /// ab <c>SpeakerTurn mod n</c> reihum. Wer diese Frage nicht sprechen kann - per Joker gesperrt, getrennt -, wird
    /// übersprungen; der Nächste spricht. Rein: wer zum Team gehört, sagt der Aufrufer.
    /// </summary>
    public static class Sprecherfolge
    {
        /// <summary>Die Mitglieder in Sprecherfolge, beginnend beim Sprecher des Stands; doppelte Kennungen zählen einmal.</summary>
        public static IReadOnlyList<Guid> Folge(IEnumerable<Teammitglied> mitglieder, int turn)
        {
            ArgumentNullException.ThrowIfNull(mitglieder);

            var sortiert = mitglieder
                .DistinctBy(m => m.Id)
                .OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(m => m.Id)
                .Select(m => m.Id)
                .ToList();

            if (sortiert.Count == 0)
                return sortiert;

            // Auch ein negativer Stand führt auf einen gültigen Platz - der Server weist ihn ab, gerechnet wird trotzdem.
            var start = (turn % sortiert.Count + sortiert.Count) % sortiert.Count;

            return [.. sortiert.Skip(start), .. sortiert.Take(start)];
        }

        /// <summary>Der Sprecher: der Erste der Folge, der nicht ausgelassen ist; <c>null</c>, wenn keiner bleibt.</summary>
        public static Guid? Sprecher(IReadOnlyList<Guid> folge, IReadOnlySet<Guid>? ausgelassen = null)
        {
            ArgumentNullException.ThrowIfNull(folge);

            foreach (var id in folge)
            {
                if (ausgelassen?.Contains(id) != true)
                    return id;
            }

            return null;
        }

        /// <summary>„Sprecher weiter“ und der Schritt nach einer bewerteten Teamregel-Zelle.</summary>
        public static int Weiter(int turn) => turn is >= 0 and < int.MaxValue ? turn + 1 : 0;

        /// <summary>„Sprecher zurück“: vom Anfang aus zum Letzten der Folge, nie unter 0.</summary>
        public static int Zurueck(int turn, int mitglieder) => turn > 0 ? turn - 1 : Math.Max(mitglieder - 1, 0);
    }
}
