using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.Logic.Demo
{
    public static partial class DemoDataRemover
    {
        /// <summary>Was entfernt wurde.</summary>
        /// <param name="Spiele">Anzahl geloeschter Spiele.</param>
        /// <param name="Fragen">Anzahl geloeschter Fragen.</param>
        /// <param name="Mitspieler">Anzahl geloeschter Mitspieler.</param>
        /// <param name="Kategorien">Anzahl geloeschter Kategorien.</param>
        /// <param name="BehalteneKategorien">
        /// Anzahl der Demo-Kategorien, die stehen bleiben mussten, weil noch eine fremde Frage
        /// darin liegt.
        /// </param>
        public sealed record Ergebnis(
            int Spiele, int Fragen, int Mitspieler, int Kategorien, int BehalteneKategorien = 0);

        /// <summary>
        /// Entfernt alles, was <see cref="DemoDataSeeder.Marke"/> in der Bezeichnung traegt.
        /// <para>
        /// <b>Die Reihenfolge ist nicht beliebig.</b> Eine Spielfeldzelle verweist mit NO ACTION
        /// auf ihre Frage - solange ein Spiel steht, laesst sich die Frage nicht loeschen. Also
        /// zuerst die Spiele (die raeumen ueber <c>GamesController.BeforeActionAsync</c> ihre
        /// Zellen, Kopfzeilen, Zuordnungen und Ergebnisse selbst weg), dann die Fragen, dann die
        /// Mitspieler, zuletzt die Kategorien.
        /// </para>
        /// <para>
        /// Bestehende Daten ohne die Marke bleiben unberuehrt.
        /// </para>
        /// </summary>
        public static async Task<Ergebnis> RemoveAsync()
        {
            var spiele = await LoescheSpieleAsync();
            var fragen = await LoescheFragenAsync();
            var spieler = await LoescheMitspielerAsync();
            var (kategorien, behalten) = await LoescheKategorienAsync();

            return new Ergebnis(spiele, fragen, spieler, kategorien, behalten);
        }

        private static bool IstDemo(string? bezeichnung)
            => bezeichnung?.StartsWith(DemoDataSeeder.Marke, StringComparison.Ordinal) == true;

        private static async Task<int> LoescheSpieleAsync()
        {
            using var ctrl = new GamesController();

            var treffer = (await ctrl.GetAllAsync()).Where(g => IstDemo(g.Designation)).ToList();

            foreach (var spiel in treffer)
                await ctrl.DeleteAsync(spiel.Id);

            if (treffer.Count > 0)
                await ctrl.SaveChangesAsync();

            return treffer.Count;
        }

        private static async Task<int> LoescheFragenAsync()
        {
            using var ctrl = new QuestionBasesController();

            var treffer = (await ctrl.GetAllAsync()).Where(q => IstDemo(q.Designation)).ToList();

            foreach (var frage in treffer)
                await ctrl.DeleteAsync(frage.Id);

            if (treffer.Count > 0)
                await ctrl.SaveChangesAsync();

            return treffer.Count;
        }

        private static async Task<int> LoescheMitspielerAsync()
        {
            using var ctrl = new PlayersController();

            var treffer = (await ctrl.GetAllAsync()).Where(p => IstDemo(p.Designation)).ToList();

            foreach (var spieler in treffer)
                await ctrl.DeleteAsync(spieler.Id);

            if (treffer.Count > 0)
                await ctrl.SaveChangesAsync();

            return treffer.Count;
        }

        /// <summary>
        /// Loescht die Demo-Kategorien - aber nur die leeren.
        /// <para>
        /// <b>Gemessen am 2026-09-06 (Befund B46):</b> der Fremdschluessel
        /// <c>FK_QuestionBase_Category_CategoryId</c> steht auf <c>CASCADE</c>. Eine
        /// <b>echte</b> Frage, die der Spielleiter in eine Demo-Kategorie gelegt hat, waere hier
        /// stillschweigend mitgeloescht worden - ohne Rueckfrage, ohne Meldung, mit ihren
        /// Schritten und Ergebniszeilen.
        /// </para>
        /// <para>
        /// Die eigenen Fragen sind zu diesem Zeitpunkt schon weg. Was noch in einer
        /// Demo-Kategorie liegt, gehoert also jemand anderem - und dann bleibt die Kategorie
        /// stehen. Ein Rest, den der Nutzer sieht, ist besser als eine Frage, die er nicht mehr
        /// findet.
        /// </para>
        /// </summary>
        private static async Task<(int Geloescht, int Behalten)> LoescheKategorienAsync()
        {
            using var ctrl = new CategoriesController();
            using var fragenCtrl = new QuestionBasesController(ctrl);

            var belegt = (await fragenCtrl.GetAllAsync())
                .Select(q => q.CategoryId)
                .Where(id => id != null)
                .Distinct()
                .ToHashSet();

            var treffer = (await ctrl.GetAllAsync()).Where(c => IstDemo(c.Designation)).ToList();
            var leer = treffer.Where(c => !belegt.Contains(c.Id)).ToList();

            foreach (var kategorie in leer)
                await ctrl.DeleteAsync(kategorie.Id);

            if (leer.Count > 0)
                await ctrl.SaveChangesAsync();

            return (leer.Count, treffer.Count - leer.Count);
        }
    }
}
