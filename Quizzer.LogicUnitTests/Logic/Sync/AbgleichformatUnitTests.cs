using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Transfer.Sync;
using Quizzer.Logic.Context;
using Quizzer.Logic.Sync;
using System.Security.Cryptography;
using System.Text;

namespace Quizzer.LogicUnitTests.Logic.Sync
{
    /// <summary>
    /// Das Abgleichformat gegen das, was der Abgleich wirklich überträgt (M37 P6, R3-4): Zeilentypen, Spalten samt Typ und die
    /// Werte jeder Enum-Spalte. Kommt etwas dazu, ohne dass <see cref="SyncSnapshot.CurrentFormatVersion"/> steigt, setzt der Push
    /// eines alten WPF still den Standardwert ein - der Versionsriegel hält nur ein anderes Format an. Vorher maßen fünf Tests die
    /// Konstante an sich selbst (<c>format &gt;= 25</c> …) und blieben bei jedem vergessenen Sprung grün.
    /// </summary>
    [TestClass]
    public class AbgleichformatUnitTests
    {
        /// <summary>
        /// Je Format der Fingerabdruck dessen, was der Abgleich überträgt. <b>Nur anfügen, nie ändern</b> - ein geänderter
        /// Eintrag ist genau der vergessene Formatsprung, den diese Prüfung fangen soll. Erfasst seit Format 33.
        /// </summary>
        private static readonly Dictionary<int, string> FingerabdruckJeFormat = new()
        {
            [33] = "63810437ED535505",
        };

        /// <summary>Eine Zeile je Spalte: Typ, Spalte, Spaltentyp, bei Enums alle Werte - geordnet, damit nur Inhalt zählt.</summary>
        internal static string Beschreibung()
        {
            using var context = new DataContext();
            var zeilen = new List<string>();

            var typen = context.Model.GetEntityTypes()
                .Select(t => t.ClrType)
                .Where(t => SyncModel.TryGet(t.Name, out _))
                .OrderBy(t => t.Name, StringComparer.Ordinal);

            foreach (var typ in typen)
            {
                zeilen.Add(typ.Name);

                foreach (var spalte in SyncRowConverter.SyncedProperties(context.Model, typ))
                {
                    var leer = Nullable.GetUnderlyingType(spalte.ClrType);
                    var art = leer ?? spalte.ClrType;
                    var werte = art.IsEnum
                        ? " {" + string.Join(",", Enum.GetValues(art).Cast<object>().Select(w => $"{w}={Convert.ToInt64(w)}")) + "}"
                        : "";

                    zeilen.Add($"  {spalte.Name}: {art.Name}{(leer != null ? "?" : "")}{werte}");
                }
            }

            return string.Join("\n", zeilen);
        }

        private static string Fingerabdruck(string beschreibung)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(beschreibung)))[..16];

        [TestMethod]
        public void TheFormatRisesExactlyWhenTheSyncCarriesSomethingNew()
        {
            var format = SyncSnapshot.CurrentFormatVersion;
            var beschreibung = Beschreibung();
            var aktuell = Fingerabdruck(beschreibung);

            Console.WriteLine($"Format {format}, Fingerabdruck {aktuell}:\n{beschreibung}");

            Assert.IsTrue(FingerabdruckJeFormat.TryGetValue(format, out var erwartet),
                $"Für Format {format} steht kein Fingerabdruck da - eintragen: [{format}] = \"{aktuell}\".");
            Assert.AreEqual(erwartet, aktuell,
                $"Der Abgleich überträgt etwas anderes als bei Format {format} (neue Spalte, neuer Typ, neuer Enum-Wert?). "
                + $"SyncSnapshot.CurrentFormatVersion hochzählen und [{format + 1}] = \"{aktuell}\" anfügen - den Eintrag für {format} nie ändern.");
        }

        /// <summary>Ein Formatsprung nur, wenn der Abgleich Neues überträgt: zwei Formate mit demselben Inhalt sind ein Sprung zu viel.</summary>
        [TestMethod]
        public void NoTwoFormatsCarryTheSame()
        {
            var doppelt = FingerabdruckJeFormat.GroupBy(e => e.Value).Where(g => g.Count() > 1).Select(g => string.Join("/", g.Select(e => e.Key))).ToList();

            Assert.AreEqual(0, doppelt.Count, "Diese Formate übertragen dasselbe: " + string.Join(", ", doppelt));
            Assert.AreEqual(SyncSnapshot.CurrentFormatVersion, FingerabdruckJeFormat.Keys.Max(), "Der jüngste Eintrag gehört zum aktuellen Format.");
        }
    }
}
