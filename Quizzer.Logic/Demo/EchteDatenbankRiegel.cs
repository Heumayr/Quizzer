using Quizzer.Logic.Context;

namespace Quizzer.Logic.Demo
{
    /// <summary>
    /// Der Riegel vor den Demobefehlen von <c>Quizzer.EF</c> (M37 P1, V29). Ohne <c>appsettings.json</c> gilt die Vorgabe, die
    /// echte Spieldatenbank <c>Quizzer</c> - ein frischer Arbeitsbaum träfe mit jedem <c>demo-*</c> den Bestand. Gewollt ist
    /// das weiterhin (Demoabend, Vorrat für den eigenen Bestand), aber nur mit <see cref="Schalter"/>. <c>database update</c>
    /// und die EF-Werkzeuge gehen hier nicht durch.
    /// </summary>
    public static class EchteDatenbankRiegel
    {
        /// <summary>Die ausdrückliche Bestätigung, dass der Befehl in die echte Datenbank schreiben soll.</summary>
        public const string Schalter = "--echte-datenbank";

        /// <summary>
        /// Die Meldung, wenn ein <c>demo-*</c>-Befehl ohne <see cref="Schalter"/> auf eine Datenbank namens <c>Quizzer</c>
        /// zielt; sonst <c>null</c>. Eine unlesbare Verbindung ergibt die Vorgabe und zählt damit als echte (fail-closed).
        /// </summary>
        public static string? Pruefe(IReadOnlyList<string> args, string? verbindung)
        {
            ArgumentNullException.ThrowIfNull(args);

            if (args.Count == 0 || !args[0].StartsWith("demo-", StringComparison.OrdinalIgnoreCase))
                return null;

            if (args.Skip(1).Contains(Schalter, StringComparer.OrdinalIgnoreCase))
                return null;

            var angaben = Datenbankangaben.Zerlege(verbindung);

            if (!string.Equals(angaben.Datenbank.Trim(), Datenbankangaben.VorgabeDatenbank, StringComparison.OrdinalIgnoreCase))
                return null;

            return $"Abgebrochen: „{args[0]}“ zielt auf die echte Spieldatenbank „{angaben.Datenbank}“ ({angaben.Server}) - "
                + "dort liegt der Bestand des Einzelbetriebs." + Environment.NewLine
                + $"Ist das gewollt, etwa für den Demoabend oder den Minispiel-Vorrat, den Befehl mit {Schalter} aufrufen:"
                + Environment.NewLine
                + $"  dotnet run --project Quizzer.EF -- {args[0]} {Schalter}" + Environment.NewLine
                + "Sonst eine andere Datenbank in AppSettings:MsSqlConString der appsettings.json neben Quizzer.EF eintragen.";
        }
    }
}
