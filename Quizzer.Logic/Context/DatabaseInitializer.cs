using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Quizzer.Logic.Context
{
    /// <summary>
    /// Aufbau und Zuruecksetzen des Datenbankschemas. Bis hierher gab es dafuer keinen
    /// Einstiegspunkt - die Datenbank musste von Hand ueber die EF-Werkzeuge gezogen werden.
    /// </summary>
    public static class DatabaseInitializer
    {
        /// <summary>
        /// Kennung, an der eine Testdatenbank erkannt wird. Nur Datenbanken mit dieser Endung
        /// duerfen geloescht werden.
        /// </summary>
        internal const string TestDatabaseSuffix = "_Tests";

        /// <summary>Name der Produktivdatenbank, die niemals fallen darf.</summary>
        internal const string ProductionDatabaseName = "Quizzer";

        /// <summary>
        /// Bringt die Datenbank auf den Stand der Migrationen, ohne etwas zu loeschen.
        /// Gefahrlos und deshalb oeffentlich.
        /// </summary>
        public static void EnsureMigrated()
        {
            using var context = new DataContext();
            context.Database.Migrate();
        }

        /// <summary>
        /// Loescht die Datenbank und baut sie aus der Migrationskette neu auf.
        /// <para>
        /// Absichtlich <c>internal</c> und absichtlich mit einer harten Sperre davor: die
        /// <c>appsettings.json</c> von <c>Quizzer.DataModels</c> zeigt auf die echte Datenbank und
        /// wird in jedes Ausgabeverzeichnis mitkopiert. Ohne diese Pruefung wuerde ein
        /// falsch eingestellter Testlauf den Spielbestand loeschen.
        /// </para>
        /// </summary>
        internal static void RecreateForTests()
        {
            GuardIsTestDatabase(DataModels.Settings.ConnectionString);

            using var context = new DataContext();
            context.Database.EnsureDeleted();
            context.Database.Migrate();
        }

        /// <summary>
        /// Wirft, wenn die Verbindungszeichenfolge nicht auf eine oertliche Testdatenbank zeigt.
        /// Bewusst <see cref="InvalidOperationException"/> statt einer Zusicherung - eine
        /// Zusicherung laesst sich abschalten, diese Sperre nicht.
        /// </summary>
        internal static void GuardIsTestDatabase(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException(
                    "Keine Verbindungszeichenfolge gesetzt. Das Loeschen wurde abgebrochen.");

            SqlConnectionStringBuilder builder;

            try
            {
                builder = new SqlConnectionStringBuilder(connectionString);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Verbindungszeichenfolge nicht lesbar. Das Loeschen wurde abgebrochen.", ex);
            }

            var database = builder.InitialCatalog;

            if (string.IsNullOrWhiteSpace(database))
                throw new InvalidOperationException(
                    "Verbindungszeichenfolge nennt keine Datenbank. Das Loeschen wurde abgebrochen.");

            if (string.Equals(database, ProductionDatabaseName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"'{database}' ist die Produktivdatenbank. Das Loeschen wurde abgebrochen.");

            if (!database.EndsWith(TestDatabaseSuffix, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"'{database}' endet nicht auf '{TestDatabaseSuffix}'. Das Loeschen wurde abgebrochen.");

            if (!builder.DataSource.Contains("(localdb)", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"'{builder.DataSource}' ist keine oertliche LocalDB. Das Loeschen wurde abgebrochen.");
        }
    }
}
