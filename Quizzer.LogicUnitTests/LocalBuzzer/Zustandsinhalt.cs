using System.Text.Json;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// Was ein Zustand ans Telefon traegt, entschluesselt (M35 C5): alle Zahlen, alle Zeichenketten und alle
    /// Feldnamen. Im rohen JSON steht „+" als <c>+</c> - ein Textvergleich darauf findet nie etwas.
    /// </summary>
    internal sealed class Zustandsinhalt
    {
        private Zustandsinhalt()
        {
        }

        public List<double> Zahlen { get; } = new();

        public List<string> Texte { get; } = new();

        /// <summary>Liest den Zustand so, wie der Hub ihn serialisiert.</summary>
        public static Zustandsinhalt Von(object? zustand)
        {
            var inhalt = new Zustandsinhalt();
            inhalt.Sammle(JsonSerializer.SerializeToElement(zustand, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            return inhalt;
        }

        /// <summary>Ob irgendeine Zeichenkette oder ein Feldname <paramref name="teil"/> enthaelt.</summary>
        public bool EnthaeltText(string teil) => Texte.Any(t => t.Contains(teil, StringComparison.Ordinal));

        private void Sammle(JsonElement e)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Number:
                    Zahlen.Add(e.GetDouble());
                    break;
                case JsonValueKind.String:
                    Texte.Add(e.GetString()!);
                    break;
                case JsonValueKind.Object:
                    foreach (var p in e.EnumerateObject())
                    {
                        Texte.Add(p.Name);
                        Sammle(p.Value);
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var x in e.EnumerateArray())
                        Sammle(x);
                    break;
            }
        }
    }
}
