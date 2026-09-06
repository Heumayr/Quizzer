using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Themes;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.Logic.Demo
{
    public static partial class DemoDataSeeder
    {
        /// <summary>Der Ordner, in dem die Texturen des Demo-Designs liegen.</summary>
        public const string DemoThemeFolder = "Abendrot";

        /// <summary>
        /// Legt ein zweites Design an, damit der Unterschied zum mitgelieferten sichtbar ist.
        /// <para>
        /// Die Texturen im Ordner werden <b>nicht</b> erzeugt - sie liegen dort schon oder eben
        /// nicht. Fehlt eine, faellt das Design fuer diese eine Textur auf den
        /// Auslieferungsstand zurueck; genau so ist es gedacht.
        /// </para>
        /// </summary>
        /// <returns>Das angelegte Design, oder <c>null</c>, wenn es schon eines gab.</returns>
        public static async Task<GameTheme?> CreateThemeAsync()
        {
            using var ctrl = new GameThemesController();

            var vorhanden = (await ctrl.GetAllAsync())
                .Any(t => string.Equals(t.FolderName, DemoThemeFolder, StringComparison.OrdinalIgnoreCase));

            if (vorhanden)
                return null;

            var eigene = ThemeAssets.Texturen.Count(t => ThemeAssets.IsOwn(DemoThemeFolder, t.Dateiname));

            var design = new GameTheme
            {
                Id = Guid.NewGuid(),
                Designation = "Abendrot",
                FolderName = DemoThemeFolder,
                Notes = $"Warme Töne. {eigene} von {ThemeAssets.Texturen.Count} Texturen eigen, "
                      + "der Rest kommt aus dem mitgelieferten Design.",
            };

            await ctrl.InsertAsync(design);
            await ctrl.SaveChangesAsync();

            return design;
        }
    }
}
