using Microsoft.Data.SqlClient;
using Quizzer.DataModels;
using Quizzer.Logic.Context;
using System.IO;
using System.Text.RegularExpressions;

namespace Quizzer.Server
{
    /// <summary>
    /// „Daten von diesem Rechner entfernen" (M28): alles, was Quizzer unter ProgramData abgelegt
    /// hat, dazu die Zwischendatenbanken und gemerkten Anmeldungen des Servermodus - auf einmal.
    /// Die eigene lokale Spieldatenbank und die Einstellungen bleiben.
    /// <para>
    /// <b>Jede Stufe hat einen Riegel, und keiner verlässt sich auf den anderen:</b> gelöscht wird
    /// nur unter einer Wurzel mit genau dem erwarteten Namen, und eine Datenbank nur, wenn ihr Name
    /// die Form der Zwischendatenbank hat (<see cref="ServerSession.CacheDatabaseName"/>).
    /// </para>
    /// </summary>
    public static partial class LocalDataRemoval
    {
        /// <summary>Was entfernt wird.</summary>
        /// <param name="ProgramDataRoot">Wird geleert; muss <c>Quizzer</c> heißen.</param>
        /// <param name="ServerStateFolder">Gemerkte Anmeldungen und Abgleichstand; muss <c>server</c> heißen.</param>
        /// <param name="MasterConnectionString">Verbindung zur <c>master</c>-Datenbank der LocalDB.</param>
        /// <param name="IsCacheDatabase">Welche Datenbanken fallen.</param>
        public sealed record Targets(string ProgramDataRoot, string ServerStateFolder, string MasterConnectionString, Func<string, bool> IsCacheDatabase);

        /// <summary>Nur für Tests: statt der echten Ziele - sonst löschte ein Testlauf die Daten dessen, der ihn startet.</summary>
        internal static Func<Targets>? TargetsForTests { get; set; }

        /// <summary>Die Ziele auf diesem Rechner.</summary>
        public static Targets Current() => TargetsForTests?.Invoke() ?? new Targets(
            Settings.ProgramDataRoot,
            ServerSession.LocalFolder,
            DatabaseInitializer.WithDatabase(ServerSession.LocalConnectionString ?? Settings.ConnectionString, "master"),
            IsCacheDatabaseName);

        [GeneratedRegex("^Quizzer_Server_[0-9a-f]{12}$")]
        private static partial Regex CacheDatabasePattern();

        /// <summary>Nur Zwischendatenbanken: <c>Quizzer_Server_</c> und 12 kleine Hex-Zeichen, sonst nichts.</summary>
        public static bool IsCacheDatabaseName(string name) => CacheDatabasePattern().IsMatch(name);

        /// <summary>Entfernt alles. Liefert, was sich nicht entfernen ließ - leer heißt: alles weg.</summary>
        public static async Task<List<string>> RemoveAsync(Targets ziele)
        {
            var fehler = new List<string>();

            fehler.AddRange(await DropCacheDatabasesAsync(ziele));
            fehler.AddRange(DeleteContents(ziele.ServerStateFolder, "server"));
            fehler.AddRange(DeleteContents(ziele.ProgramDataRoot, "Quizzer"));

            return fehler;
        }

        private static async Task<List<string>> DropCacheDatabasesAsync(Targets ziele)
        {
            var fehler = new List<string>();

            // Eine offene Verbindung aus dem eigenen Pool hielte die Zwischendatenbank fest.
            SqlConnection.ClearAllPools();

            try
            {
                await using var verbindung = new SqlConnection(ziele.MasterConnectionString);
                await verbindung.OpenAsync();

                var namen = new List<string>();
                await using (var abfrage = new SqlCommand("SELECT name FROM sys.databases", verbindung))
                await using (var leser = await abfrage.ExecuteReaderAsync())
                {
                    while (await leser.ReadAsync())
                        namen.Add(leser.GetString(0));
                }

                foreach (var name in namen.Where(ziele.IsCacheDatabase))
                {
                    try
                    {
                        // Der Name kommt als Parameter und geht nur durch QUOTENAME in den Befehl.
                        await using var loeschen = new SqlCommand(
                            "DECLARE @s nvarchar(max) = N'ALTER DATABASE ' + QUOTENAME(@name) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ' + QUOTENAME(@name); EXEC (@s);",
                            verbindung);
                        loeschen.Parameters.AddWithValue("@name", name);
                        await loeschen.ExecuteNonQueryAsync();
                    }
                    catch (SqlException ex)
                    {
                        fehler.Add($"Datenbank {name}: {ex.Message}");
                    }
                }
            }
            catch (SqlException ex)
            {
                fehler.Add("Die lokale Datenbank war nicht erreichbar, Zwischendatenbanken blieben: " + ex.Message);
            }

            return fehler;
        }

        /// <summary>
        /// Leert <paramref name="wurzel"/>, die Wurzel selbst bleibt. Heißt sie anders als
        /// <paramref name="erwarteterName"/>, wird nichts angefasst - ein verbogener Pfad soll nicht
        /// den Ordner darüber leeren.
        /// </summary>
        private static List<string> DeleteContents(string wurzel, string erwarteterName)
        {
            var fehler = new List<string>();
            var voll = Path.TrimEndingDirectorySeparator(Path.GetFullPath(wurzel));

            if (!string.Equals(Path.GetFileName(voll), erwarteterName, StringComparison.OrdinalIgnoreCase) || Path.GetPathRoot(voll) == voll)
            {
                fehler.Add($"{voll}: heißt nicht „{erwarteterName}“ - nicht angefasst.");
                return fehler;
            }

            if (!Directory.Exists(voll))
                return fehler;

            foreach (var eintrag in Directory.EnumerateFileSystemEntries(voll))
            {
                try
                {
                    if (Directory.Exists(eintrag))
                        Directory.Delete(eintrag, recursive: true);
                    else
                        File.Delete(eintrag);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    fehler.Add($"{eintrag}: {ex.Message}");
                }
            }

            return fehler;
        }
    }
}
