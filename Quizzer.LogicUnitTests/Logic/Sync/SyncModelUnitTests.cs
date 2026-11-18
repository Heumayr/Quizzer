using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Auth;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Context;
using Quizzer.Logic.Sync;

namespace Quizzer.LogicUnitTests.Logic.Sync
{
    /// <summary>
    /// Die Positivliste des Abgleichs gegen das Datenmodell (M37 T1). Eine neue Tabelle, die in <see cref="SyncModel"/>
    /// fehlt, wandert still nie zum Server - und ein Rang, der vor seinem Fremdschlüssel liegt, bricht das Einspielen.
    /// Beides prüfte vorher nichts.
    /// </summary>
    [TestClass]
    public class SyncModelUnitTests
    {
        /// <summary>Was absichtlich nie wandert - mit Grund (<c>SyncModel</c>, Kopf).</summary>
        private static readonly Dictionary<Type, string> NieImAbgleich = new()
        {
            [typeof(Account)] = "Konten verwaltet der Server selbst",
            [typeof(RefreshToken)] = "Anmeldung, gehört in keine Antwort",
            [typeof(AccountRecoveryCode)] = "Anmeldung, gehört in keine Antwort",
        };

        private static IEnumerable<Microsoft.EntityFrameworkCore.Metadata.IEntityType> Tabellen()
        {
            using var context = new DataContext();
            return context.Model.GetEntityTypes().ToList();
        }

        [TestMethod]
        public void EveryTableOfTheModelIsSyncedOrDeliberatelyNot()
        {
            var fehlend = Tabellen()
                .Select(t => t.ClrType)
                .Where(t => !NieImAbgleich.ContainsKey(t) && !SyncModel.TryGet(t.Name, out _))
                .Select(t => t.Name)
                .ToList();

            Assert.AreEqual(0, fehlend.Count, "Diese Tabellen fehlen in SyncModel.Entries und wandern nie zum Server: " + string.Join(", ", fehlend));
        }

        /// <summary>
        /// Jeder Fremdschlüssel zwischen zwei Abgleich-Typen zeigt auf einen kleineren Rang - sonst spielte der Server den
        /// Platz vor seinem Team ein. Ausgenommen sind Typen derselben Wurzel (TPT der Fragen).
        /// </summary>
        [TestMethod]
        public void EveryForeignKeyPointsToAnEarlierRank()
        {
            var verstoesse = new List<string>();

            foreach (var tabelle in Tabellen())
            {
                if (!SyncModel.TryGet(tabelle.ClrType.Name, out var kind))
                    continue;

                foreach (var schluessel in tabelle.GetForeignKeys())
                {
                    if (!SyncModel.TryGet(schluessel.PrincipalEntityType.ClrType.Name, out var eltern) || eltern.RootType == kind.RootType)
                        continue;

                    if (eltern.Rank >= kind.Rank)
                        verstoesse.Add($"{kind.Name} (Rang {kind.Rank}) zeigt auf {eltern.Name} (Rang {eltern.Rank})");
                }
            }

            Assert.AreEqual(0, verstoesse.Count, string.Join(Environment.NewLine, verstoesse));
        }

        /// <summary>Die Gegenprobe: die Regel sieht die Fremdschlüssel der Teams überhaupt.</summary>
        [TestMethod]
        public void TheTeamForeignKeysAreSeen()
        {
            var aufTeams = Tabellen()
                .SelectMany(t => t.GetForeignKeys())
                .Count(k => k.PrincipalEntityType.ClrType == typeof(GameTeam));

            Assert.AreEqual(2, aufTeams, "Platz und Ergebnis zeigen auf das Team.");
        }
    }
}
