using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;

namespace Quizzer.Logic.Sync
{
    /// <summary>
    /// Die Positivliste des Abgleichs: welche Entitaetstypen zwischen Server und WPF wandern, in
    /// welcher Reihenfolge sie eingespielt werden und welche Felder nie mitgehen.
    /// <para>
    /// <b>Was hier fehlt, wandert nicht.</b> <c>Account</c> und <c>RefreshToken</c> stehen
    /// absichtlich nicht darin; ein Zeilentyp, den diese Liste nicht kennt, wird abgewiesen.
    /// </para>
    /// </summary>
    public static class SyncModel
    {
        /// <summary>Ein Typ im Abgleich und sein Rang: kleinere Raenge werden zuerst eingespielt.</summary>
        public sealed record Entry(Type ClrType, int Rank, Type RootType)
        {
            public string Name => ClrType.Name;
        }

        public static IReadOnlyList<Entry> Entries { get; } =
        [
            new(typeof(Category), 0, typeof(Category)),
            new(typeof(GameTheme), 0, typeof(GameTheme)),
            new(typeof(Player), 1, typeof(Player)),
            new(typeof(QuestionBase), 2, typeof(QuestionBase)),
            new(typeof(DefaultQuestion), 2, typeof(QuestionBase)),
            new(typeof(MultipleChoiceQuestion), 2, typeof(QuestionBase)),
            new(typeof(PropertiesQuestion), 2, typeof(QuestionBase)),
            new(typeof(AppreciateQestion), 2, typeof(QuestionBase)),
            new(typeof(RevealQuestion), 2, typeof(QuestionBase)),
            new(typeof(OrderingQuestion), 2, typeof(QuestionBase)),
            new(typeof(MatchingQuestion), 2, typeof(QuestionBase)),
            new(typeof(QuestionStepResource), 3, typeof(QuestionStepResource)),
            new(typeof(Game), 4, typeof(Game)),
            new(typeof(Header), 5, typeof(Header)),
            new(typeof(GameGridCoordinate), 5, typeof(GameGridCoordinate)),
            new(typeof(PlayerXGame), 5, typeof(PlayerXGame)),
            new(typeof(QuestionResult), 6, typeof(QuestionResult)),
        ];

        private static readonly Dictionary<string, Entry> ByName =
            Entries.ToDictionary(e => e.Name, StringComparer.Ordinal);

        /// <summary>
        /// Felder, die nie im Abgleich stehen. <c>Player.PasswordHash</c> ist das lokale
        /// Spielleiter-Kennwort aus der Zeit vor dem Server; am Server meldet man sich ueber ein
        /// Konto an, und ein Hash gehoert in keine Antwort (standards-sicherheit A04).
        /// </summary>
        private static readonly HashSet<(Type, string)> ExcludedProperties =
        [
            (typeof(Player), nameof(Player.PasswordHash)),
        ];

        public static bool TryGet(string typeName, out Entry entry) => ByName.TryGetValue(typeName, out entry!);

        public static Entry Get(string typeName)
            => TryGet(typeName, out var entry)
                ? entry
                : throw new SyncFormatException($"Unbekannter Zeilentyp '{typeName}'.");

        public static Entry For(Type clrType)
            => ByName.TryGetValue(clrType.Name, out var entry) && entry.ClrType == clrType
                ? entry
                : throw new SyncFormatException($"Der Typ '{clrType.FullName}' gehört nicht zum Abgleich.");

        public static bool IsExcluded(Type declaringOrEntityType, string propertyName)
        {
            for (var t = declaringOrEntityType; t != null; t = t.BaseType)
            {
                if (ExcludedProperties.Contains((t, propertyName)))
                    return true;
            }

            return false;
        }

        public static bool IsQuestion(Entry entry) => entry.RootType == typeof(QuestionBase);
    }

    /// <summary>Eine Zeile passt nicht zum Abgleich - unbekannter Typ, Feld oder Wert.</summary>
    public sealed class SyncFormatException(string message, Exception? inner = null) : Exception(message, inner);
}
