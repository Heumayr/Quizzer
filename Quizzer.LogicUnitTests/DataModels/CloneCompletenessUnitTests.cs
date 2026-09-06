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
        /// <para>
        /// <b>Deshalb wird der Ist-Zustand hereingereicht.</b> Bis 2026-09-07 waehlte diese
        /// Stelle absolut - <c>true</c> fuer jedes <c>bool</c> und den zweitkleinsten Zahlenwert
        /// fuer jedes Enum. Vier Properties tragen aber genau diesen Wert schon als
        /// Feldinitialisierer (<c>Difficulty = Level1</c>, das <b>ist</b> der zweite Enumwert;
        /// <c>WarnOnFinishStep = true</c>, <c>ShowTextOnKeySelect = true</c>,
        /// <c>IsModerator = true</c>). Der Test setzte dort also das, was schon dastand, und der
        /// Klon hat denselben Initialisierer - der Vergleich konnte gar nicht scheitern.
        /// </para>
        /// </summary>
        private static object? DistinctValue(Type type, int seed, object? aktuell = null)
        {
            var kern = Nullable.GetUnderlyingType(type) ?? type;

            if (kern.IsEnum)
            {
                var werte = Enum.GetValues(kern).Cast<object>().ToList();

                // Der erste Wert, der NICHT schon dasteht.
                var anders = werte.FirstOrDefault(w => !Equals(w, aktuell));

                return anders ?? werte.FirstOrDefault();
            }

            if (kern == typeof(string)) return $"Probe-{seed}";
            if (kern == typeof(Guid)) return Guid.NewGuid();
            if (kern == typeof(bool)) return aktuell is bool b ? !b : true;
            if (kern == typeof(DateTime)) return new DateTime(2026, 9, 6).AddDays(seed);
            if (kern == typeof(byte[])) return new byte[] { (byte)(seed + 1), 2, 3 };

            // Zahlen: der Reihe nach ausweichen, bis der Wert nicht mehr dem Ist-Zustand
            // gleicht. Gemessen noetig - RevealQuestion.MinusPoints steht auf 50, und
            // "40 + seed" traf das bei seed = 10 genau.
            for (var versatz = 0; versatz < 4; versatz++)
            {
                var stufe = seed + versatz * 17;

                object? kandidat =
                      kern == typeof(int) ? 40 + stufe
                    : kern == typeof(long) ? 40L + stufe
                    : kern == typeof(short) ? (short)(40 + stufe)
                    : kern == typeof(byte) ? (byte)(40 + stufe)
                    : kern == typeof(double) ? 40.5 + stufe
                    : kern == typeof(float) ? 40.5f + stufe
                    : kern == typeof(decimal) ? 40.5m + stufe
                    : null;

                if (kandidat == null)
                    return null;

                if (!Equals(kandidat, aktuell))
                    return kandidat;
            }

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
                    var wert = DistinctValue(property.PropertyType, seed++, property.GetValue(original));

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

                    // Der gesetzte Wert muss sich vom Standard unterscheiden, sonst prueft der
                    // Vergleich darunter nichts. Vier Properties fielen genau hier durch.
                    var frisch = property.GetValue(Activator.CreateInstance(typ)!);

                    Assert.IsFalse(ValuesMatch(erwartet, frisch),
                        $"{typ.Name}.{property.Name}: der Probewert gleicht dem Standardwert "
                        + $"({frisch}) - eine vergessene Property waere hier nicht zu erkennen.");
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
        /// <para>
        /// <b>Bis 2026-09-07 pruefte diese Zusicherung nichts.</b> Sie klonte eine frisch
        /// erzeugte Entitaet, deren Sammlungen alle leer sind - der Klon war damit in jedem Fall
        /// leer, ob er die Referenz mitnimmt, sie neu anlegt oder gar nichts tut. Jetzt wird
        /// vorher je Sammlung ein Element hineingelegt, und eine Zaehlschranke sagt, dass
        /// ueberhaupt etwas geprueft wurde.
        /// </para>
        /// </summary>
        [TestMethod]
        public void CollectionsAreLeftBehind()
        {
            var geprueft = 0;

            foreach (var typ in EntityTypes())
            {
                var original = (ModelBase)Activator.CreateInstance(typ)!;

                var sammlungen = typ.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && p.CanWrite)
                    .Where(p => p.GetCustomAttribute<NotMappedAttribute>() == null)
                    .Where(p => p.PropertyType.IsGenericType
                             && p.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
                    .ToList();

                // ERST FUELLEN, dann klonen - sonst ist "der Klon ist leer" von "das Original
                // war leer" nicht zu unterscheiden.
                var gefuellt = new List<string>();

                foreach (var sammlung in sammlungen)
                {
                    if (sammlung.GetValue(original) is not System.Collections.IList liste)
                        continue;

                    var elementTyp = sammlung.PropertyType.GetGenericArguments()[0];

                    if (elementTyp.IsAbstract || elementTyp.GetConstructor(Type.EmptyTypes) == null)
                        continue;

                    liste.Add(Activator.CreateInstance(elementTyp)!);
                    gefuellt.Add(sammlung.Name);
                }

                var methode = typ.GetMethod(nameof(ICloneWithoutReferences<ModelBase>.CloneWithoutReferences),
                    BindingFlags.Public | BindingFlags.Instance)!;

                var klon = methode.Invoke(original, new object[] { true })!;

                foreach (var sammlung in sammlungen)
                {
                    if (!gefuellt.Contains(sammlung.Name))
                        continue;

                    Assert.AreEqual(1, ((System.Collections.ICollection)sammlung.GetValue(original)!).Count,
                        $"{typ.Name}.{sammlung.Name}: das Original wurde beim Klonen selbst "
                        + "veraendert.");

                    if (sammlung.GetValue(klon) is System.Collections.ICollection liste)
                    {
                        geprueft++;

                        Assert.AreEqual(0, liste.Count,
                            $"{typ.Name}.{sammlung.Name} kommt gefuellt aus dem Klon - "
                            + "beim Speichern entstuenden doppelte Kindzeilen.");
                    }
                }
            }

            Assert.IsTrue(geprueft >= 5,
                $"Es wurden nur {geprueft} Sammlungen geprueft - dann sagt diese Zusicherung "
                + "nichts. Entweder fuellt die Probe nicht mehr, oder es gibt die Sammlungen "
                + "nicht mehr.");
        }
    }
}
