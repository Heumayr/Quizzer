namespace Quizzer.DataModels.Transfer.Server
{
    /// <summary>
    /// Wohin das WPF im Servermodus die vom Server geholten Medien legt (M26): je Server ein
    /// eigener Ordner unter ProgramData, getrennt vom Datenordner des Einzelbetriebs.
    /// </summary>
    public static class ServerMediaFolder
    {
        /// <summary>
        /// <c>%ProgramData%\Quizzer\Server\&lt;host&gt;_&lt;port&gt;</c>. Je Server einer, weil gleich
        /// benannte Design-Ordner zweier Server sonst ineinanderliefen - der Abgleich ersetzt eine
        /// vorhandene Datei nie.
        /// </summary>
        /// <param name="server">Die Serveradresse, mit oder ohne Pfad.</param>
        /// <param name="programData">Nur für Tests: statt <c>%ProgramData%</c>.</param>
        public static string For(Uri server, string? programData = null)
        {
            var root = programData ?? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            // Klein schreibt Uri den Namen selbst (gemessen 2026-10-23: ohne eigenes Kleinschreiben
            // blieb die Zusicherung dafuer gruen). Eine IPv6-Adresse traegt aber Doppelpunkte - die
            // gehoeren in keinen Ordnernamen.
            var host = new string(server.IdnHost
                .Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' ? c : '-')
                .ToArray());

            return Path.Combine(root, "Quizzer", "Server", $"{host}_{server.Port}");
        }
    }
}
