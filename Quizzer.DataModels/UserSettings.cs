using System.Text.Json;

namespace Quizzer.DataModels
{
    /// <summary>
    /// Die Einstellungen, die der Nutzer selbst setzt - und die die ausgelieferten Vorgaben
    /// überschreiben.
    /// <para>
    /// <b>Nutzerentscheidung vom 2026-09-06:</b> „wo das programm die ressourcen ablegt muss auch
    /// im programm konfigurierbar sein ... eigentlich alle einstellungen". Der Anlass war das
    /// Weitergeben: „damit ich das programm weitergeben kann ohne großen aufwand für den
    /// endnutzer".
    /// </para>
    /// <para>
    /// <b>Warum nicht die <c>appsettings.json</c>:</b> die liegt im Programmordner, und dort darf
    /// ein Endnutzer nicht schreiben. Diese Datei liegt unter <c>%APPDATA%\Quizzer</c> und gehört
    /// ihm.
    /// </para>
    /// <para>
    /// <b>Sie wird nie von selbst angewandt.</b> <see cref="Settings.LoadSettings"/> lädt weiter
    /// nur die Vorgaben; die Überschreibung ist ein eigener Aufruf aus der Anwendung heraus. Der
    /// Grund ist gemessen: <c>DataContext</c> lädt die Einstellungen ebenfalls, und ein Testlauf,
    /// der dabei die Werte des Nutzers erwischt, schreibt in dessen <b>echte</b> Spieldatenbank.
    /// </para>
    /// </summary>
    public static class UserSettings
    {
        /// <summary>Was in der Datei steht. Ein leeres Feld heißt: die Vorgabe gilt weiter.</summary>
        /// <param name="FilePathQuizzer">Der Ordner für Bilder und Medien.</param>
        /// <param name="MsSqlConString">Die Verbindungszeichenfolge zur Datenbank.</param>
        public sealed record Werte(string? FilePathQuizzer, string? MsSqlConString);

        private static string? folderOverride;

        /// <summary>
        /// Der Ordner, in dem die Datei liegt - er gehört dem angemeldeten Windows-Nutzer.
        /// <para>
        /// <b>Für Tests umlenkbar</b>, und das ist keine Bequemlichkeit: eine Zusicherung, die
        /// hier schreibt, überschriebe sonst die echten Einstellungen dessen, der den Testlauf
        /// startet.
        /// </para>
        /// </summary>
        public static string Folder => folderOverride ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Quizzer");

        /// <summary>Nur für Tests: lenkt die Ablage in einen Wegwerfordner um.</summary>
        internal static void UseFolderForTests(string? ordner) => folderOverride = ordner;

        /// <summary>Die Datei selbst.</summary>
        public static string FilePath => Path.Combine(Folder, "settings.json");

        /// <summary>Ob der Nutzer überhaupt schon eigene Werte gesetzt hat.</summary>
        public static bool Exists() => File.Exists(FilePath);

        /// <summary>
        /// Liest die eigenen Werte. Gibt leere Felder zurück, wenn es keine Datei gibt oder sie
        /// unlesbar ist.
        /// <para>
        /// <b>Eine unlesbare Datei wirft nicht.</b> Sie ist von Hand bearbeitbar, und ein
        /// Tippfehler darin darf das Programm nicht am Starten hindern - sonst kommt der Nutzer
        /// an die Maske nicht mehr heran, mit der er ihn beheben würde.
        /// </para>
        /// </summary>
        public static Werte Load()
        {
            if (!Exists())
                return new Werte(null, null);

            try
            {
                var text = File.ReadAllText(FilePath);
                var gelesen = JsonSerializer.Deserialize<Werte>(text);

                return gelesen ?? new Werte(null, null);
            }
            catch (Exception)
            {
                return new Werte(null, null);
            }
        }

        /// <summary>
        /// Legt die eigenen Werte über die Vorgaben. Ein leeres Feld lässt die Vorgabe stehen.
        /// </summary>
        public static void Apply()
        {
            var werte = Load();

            if (!string.IsNullOrWhiteSpace(werte.FilePathQuizzer))
                Settings.FilePathQuizzer = werte.FilePathQuizzer;

            if (!string.IsNullOrWhiteSpace(werte.MsSqlConString))
                Settings.ConnectionString = werte.MsSqlConString;
        }

        /// <summary>
        /// Schreibt die eigenen Werte und wendet sie sofort an. Der Ordner entsteht dabei, wenn
        /// es ihn noch nicht gibt.
        /// </summary>
        public static void Save(string? filePathQuizzer, string? msSqlConString)
        {
            Directory.CreateDirectory(Folder);

            var werte = new Werte(
                string.IsNullOrWhiteSpace(filePathQuizzer) ? null : filePathQuizzer.Trim(),
                string.IsNullOrWhiteSpace(msSqlConString) ? null : msSqlConString.Trim());

            File.WriteAllText(
                FilePath,
                JsonSerializer.Serialize(werte, new JsonSerializerOptions { WriteIndented = true }));

            Apply();
        }
    }
}
