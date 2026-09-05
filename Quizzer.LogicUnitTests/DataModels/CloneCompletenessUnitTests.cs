using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace Quizzer.LogicUnitTests.DataModels
{
    /// <summary>
    /// Die Vollstaendigkeit der Klonmethoden - fuer jedes Modell, ohne dass jemand daran denken muss.
    /// <para>
    /// Der gesamte Schreibpfad geht ueber <c>CloneWithoutReferences</c>: <c>GenericController</c>
    /// schreibt nicht die uebergebene Entitaet, sondern deren Klon. <b>Eine Property, die der Klon
    /// vergisst, wird still nie gespeichert</b> - kein Fehler, keine Warnung, der Wert ist beim
    /// naechsten Laden einfach wieder der alte.
    /// </para>
    /// <para>
    /// Diese Probe fuellt jede persistierte Property mit einem vom Standardwert abweichenden Wert,
    /// klont und vergleicht. Sie ist der Grund, warum beim naechsten neuen Feld nichts passieren
    /// kann: wer es in den Klon nicht aufnimmt, sieht hier einen roten Test mit dem Namen der
    /// Property.
    /// </para>
    /// </summary>
    [TestClass]
    public class CloneCompletenessUnitTests
    {
        /// <summary>Alle anlegbaren Entitaetstypen aus <c>Quizzer.DataModels</c>.</summary>
        private static IEnumerable<Type> EntityTypes()
        {
            return typeof(ModelBase).Assembly
                .GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(ModelBase).IsAssignableFrom(t))
                .Where(t => t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t.FullName, StringComparer.Ordinal);
        }

        /// <summary>
        /// Was in die Datenbank geht: oeffentlich lesbar und schreibbar, kein
        /// <see cref="NotMappedAttribute"/>, und ein Werttyp - Navigationen und Sammlungen laesst
        /// der Klon absichtlich zurueck.
        /// </summary>
        private static IEnumerable<PropertyInfo> PersistedScalarProperties(Type type)
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite)
                .Where(p => p.GetCustomAttribute<NotMappedAttribute>() == null)
                .Where(p => IsScalar(p.PropertyType))
                .OrderBy(p => p.Name, StringComparer.Ordinal);
        }

        private static bool IsScalar(Type type)
        {
            var kern = Nullable.GetUnderlyingType(type) ?? type;

            return kern.IsEnum
                || kern.IsPrimitive
                || kern == typeof(string)
                || kern == typeof(Guid)
                || kern == typeof(DateTime)
                || kern == typeof(decimal)
                || kern == typeof(byte[]);
        }

        /// <summary>
        /// Ein Wert, der sich vom Standard unterscheidet. Nur dann sagt der Vergleich etwas aus:
        /// bliebe der Wert der Standardwert, waere eine vergessene Property nicht zu erkennen.
        /// </summary>
        private static object? DistinctValue(Type type, int seed)
        {
            var kern = Nullable.GetUnderlyingType(type) ?? type;

            if (kern.IsEnum)
            {
                var werte = Enum.GetValues(kern);
                return werte.Length > 1 ? werte.GetValue(1) : werte.GetValue(0);
            }

            if (kern == typeof(string)) return $"Probe-{seed}";
            if (kern == typeof(Guid)) return Guid.NewGuid();
            if (kern == typeof(bool)) return true;
            if (kern == typeof(DateTime)) return new DateTime(2026, 9, 6).AddDays(seed);
            if (kern == typeof(byte[])) return new byte[] { (byte)(seed + 1), 2, 3 };

            if (kern == typeof(int)) return 40 + seed;
            if (kern == typeof(long)) return 40L + seed;
            if (kern == typeof(short)) return (short)(40 + seed);
            if (kern == typeof(byte)) return (byte)(40 + seed);
            if (kern == typeof(double)) return 40.5 + seed;
            if (kern == typeof(float)) return 40.5f + seed;
            if (kern == typeof(decimal)) return 40.5m + seed;

            return null;
        }

        private static bool ValuesMatch(object? links, object? rechts)
        {
            if (links is byte[] a && rechts is byte[] b)
                return a.SequenceEqual(b);

            return Equals(links, rechts);
        }

        /// <summary>
        /// Der Kern: jede persistierte Property muss den Klon ueberleben. Der Test nennt beim
        /// Scheitern Typ und Property, damit die Stelle ohne Suchen zu finden ist.
        /// </summary>
        [TestMethod]
        public void EveryPersistedPropertySurvivesTheClone()
        {
            var luecken = new List<string>();
            var geprueft = 0;

            foreach (var typ in EntityTypes())
            {
                var original = (ModelBase)Activator.CreateInstance(typ)!;
                var properties = PersistedScalarProperties(typ).ToList();

                var seed = 0;
                var gesetzt = new List<PropertyInfo>();

                foreach (var property in properties)
                {
                    var wert = DistinctValue(property.PropertyType, seed++);

                    if (wert == null)
                        continue;

                    property.SetValue(original, wert);
                    gesetzt.Add(property);
                }

                var klon = KlonVon(original, typ);

                Assert.IsNotNull(klon, $"{typ.Name}.CloneWithoutReferences lieferte null.");

                Assert.IsInstanceOfType(klon, typ,
                    $"{typ.Name}.CloneWithoutReferences liefert {klon!.GetType().Name} - "
                    + "beim Speichern entstuende damit der falsche Typ.");

                foreach (var property in gesetzt)
                {
                    geprueft++;

                    var erwartet = property.GetValue(original);
                    var tatsaechlich = property.GetValue(klon);

                    if (!ValuesMatch(erwartet, tatsaechlich))
                        luecken.Add($"{typ.Name}.{property.Name}");
                }
            }

            Assert.IsTrue(geprueft > 40,
                $"Nur {geprueft} Properties geprueft - die Suche nach den Modellen greift nicht mehr.");

            Assert.AreEqual(0, luecken.Count,
                "Diese Properties gehen beim Klonen verloren und werden deshalb nie gespeichert: "
                + string.Join(", ", luecken)
                + ". Sie gehoeren in CloneWithoutReferences des jeweiligen Modells.");
        }

        /// <summary>
        /// Ruft <c>CloneWithoutReferences(true)</c> ueber Reflexion - die Methode ist je Typ
        /// anders typisiert, ein gemeinsames Interface mit fester Signatur gibt es nicht.
        /// </summary>
        private static object? KlonVon(ModelBase original, Type typ)
        {
            var methode = typ.GetMethod(nameof(ICloneWithoutReferences<ModelBase>.CloneWithoutReferences),
                BindingFlags.Public | BindingFlags.Instance);

            Assert.IsNotNull(methode, $"{typ.Name} hat keine Methode CloneWithoutReferences.");

            return methode!.Invoke(original, new object[] { true });
        }

        /// <summary>
        /// Ohne Identitaet geklont darf keine Id und keine Zeilenversion mitkommen - sonst
        /// ueberschriebe ein als neu gedachter Datensatz einen bestehenden.
        /// </summary>
        [TestMethod]
        public void CloningWithoutIdentityDropsIdAndRowVersion()
        {
            foreach (var typ in EntityTypes())
            {
                var original = (ModelBase)Activator.CreateInstance(typ)!;
                original.Id = Guid.NewGuid();
                original.RowVersion = new byte[] { 1, 2, 3 };

                var methode = typ.GetMethod(nameof(ICloneWithoutReferences<ModelBase>.CloneWithoutReferences),
                    BindingFlags.Public | BindingFlags.Instance)!;

                var klon = (ModelBase)methode.Invoke(original, new object[] { false })!;

                Assert.AreEqual(Guid.Empty, klon.Id,
                    $"{typ.Name} nimmt die Id mit, obwohl copyIdentity false ist.");

                Assert.IsNull(klon.RowVersion,
                    $"{typ.Name} nimmt die Zeilenversion mit, obwohl copyIdentity false ist.");
            }
        }

        /// <summary>
        /// Sammlungen bleiben zurueck - das ist Absicht und die Grundlage des Schreibpfads. Ein
        /// Klon, der sie mitnaehme, wuerde beim Speichern Kindzeilen doppeln.
        /// </summary>
        [TestMethod]
        public void CollectionsAreLeftBehind()
        {
            foreach (var typ in EntityTypes())
            {
                var original = (ModelBase)Activator.CreateInstance(typ)!;

                var methode = typ.GetMethod(nameof(ICloneWithoutReferences<ModelBase>.CloneWithoutReferences),
                    BindingFlags.Public | BindingFlags.Instance)!;

                var klon = methode.Invoke(original, new object[] { true })!;

                var sammlungen = typ.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && p.CanWrite)
                    .Where(p => p.GetCustomAttribute<NotMappedAttribute>() == null)
                    .Where(p => p.PropertyType.IsGenericType
                             && p.PropertyType.GetGenericTypeDefinition() == typeof(List<>));

                foreach (var sammlung in sammlungen)
                {
                    var wert = sammlung.GetValue(klon);

                    if (wert is System.Collections.ICollection liste)
                    {
                        Assert.AreEqual(0, liste.Count,
                            $"{typ.Name}.{sammlung.Name} kommt gefuellt aus dem Klon - "
                            + "beim Speichern entstuenden doppelte Kindzeilen.");
                    }
                }
            }
        }
    }
}
