using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Quizzer.DataModels;
using Quizzer.Logic.Context;

namespace Quizzer.Server.Infrastructure
{
    /// <summary>
    /// Migration und Betriebsanmeldung. <b>Nur der Befehl <c>migrate</c> darf das Schema aendern</b>;
    /// er laeuft mit der Admin-Anmeldung des SQL Servers. Der Server selbst laeuft danach mit
    /// einer Anmeldung, die nur lesen und schreiben darf (Plan: zwei SQL-Anmeldungen, nie
    /// <c>sa</c> im Betrieb).
    /// </summary>
    public static class DatabaseSetup
    {
        public static void UseConnection(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Keine Verbindungszeichenfolge (ConnectionStrings__Quizzer).");

            // DataContext liest die Zeichenfolge aus Settings; die statische Ladeschleife ueber
            // appsettings.json greift nur, solange sie leer ist.
            Settings.ConnectionString = connectionString;
        }

        public static async Task MigrateAsync(ServerOptions options, ILogger log, CancellationToken ct)
        {
            await using (var context = new DataContext())
            {
                var pending = (await context.Database.GetPendingMigrationsAsync(ct)).ToList();
                log.LogInformation("Offene Migrationen: {Count}", pending.Count);

                await context.Database.MigrateAsync(ct);
                log.LogInformation("Datenbank auf dem Stand der letzten Migration.");
            }

            if (string.IsNullOrWhiteSpace(options.AppLogin.Name) || string.IsNullOrWhiteSpace(options.AppLogin.Password))
            {
                log.LogWarning("Keine Betriebsanmeldung konfiguriert (Quizzer__AppLogin__Name/Password) - übersprungen.");
                return;
            }

            await EnsureAppLoginAsync(options.AppLogin.Name, options.AppLogin.Password, log, ct);
        }

        /// <summary>
        /// Legt die Betriebsanmeldung an oder setzt ihr Kennwort neu, und gibt ihr in der
        /// Datenbank genau Lesen und Schreiben. Name und Kennwort gehen als Parameter in
        /// dynamisches SQL und werden dort mit QUOTENAME eingefasst - nie als Text verkettet.
        /// </summary>
        private static async Task EnsureAppLoginAsync(string name, string password, ILogger log, CancellationToken ct)
        {
            if (name.Length > 128 || password.Length > 128)
                throw new InvalidOperationException("Name und Kennwort der Betriebsanmeldung dürfen höchstens 128 Zeichen haben.");

            const string sql = """
                DECLARE @stmt nvarchar(max);
                IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @name)
                    SET @stmt = N'CREATE LOGIN ' + QUOTENAME(@name) + N' WITH PASSWORD = ' + QUOTENAME(@password, '''') + N', CHECK_POLICY = ON';
                ELSE
                    SET @stmt = N'ALTER LOGIN ' + QUOTENAME(@name) + N' WITH PASSWORD = ' + QUOTENAME(@password, '''');
                EXEC (@stmt);

                IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @name)
                BEGIN
                    SET @stmt = N'CREATE USER ' + QUOTENAME(@name) + N' FOR LOGIN ' + QUOTENAME(@name);
                    EXEC (@stmt);
                END

                SET @stmt = N'ALTER ROLE db_datareader ADD MEMBER ' + QUOTENAME(@name);
                EXEC (@stmt);
                SET @stmt = N'ALTER ROLE db_datawriter ADD MEMBER ' + QUOTENAME(@name);
                EXEC (@stmt);
                """;

            await using var context = new DataContext();
            await context.Database.ExecuteSqlRawAsync(sql,
                [new SqlParameter("@name", name), new SqlParameter("@password", password)], ct);

            log.LogInformation("Betriebsanmeldung {Login} mit Lese- und Schreibrecht eingerichtet.", name);
        }

        /// <summary>Der Server startet nicht gegen ein Schema, das nicht zu ihm passt.</summary>
        public static async Task EnsureUpToDateAsync(CancellationToken ct)
        {
            await using var context = new DataContext();
            var pending = (await context.Database.GetPendingMigrationsAsync(ct)).ToList();

            if (pending.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Die Datenbank ist nicht auf dem Stand ({pending.Count} Migrationen offen, zuerst: {pending[0]}). "
                    + "Erst 'migrate' laufen lassen.");
            }
        }
    }
}
