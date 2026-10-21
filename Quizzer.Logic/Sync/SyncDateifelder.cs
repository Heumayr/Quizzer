using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.DataModels.Transfer.Sync;
using System.Text.Json;

namespace Quizzer.Logic.Sync
{
    /// <summary>
    /// Die Felder, die im WPF zu einem Dateipfad werden - und was in ihnen stehen darf.
    /// <para>
    /// <b>Anlass: Sicherheitsprüfung 2026-10-21</b> (beide Prüfer unabhängig). Der Abgleich nahm
    /// in diesen Feldern jeden Text an, und das WPF baut daraus mit <c>Path.Combine</c> einen
    /// Pfad - der bei einem absoluten oder UNC-Wert (<c>\\fremder-rechner\s\bild.png</c>) den
    /// Datenordner einfach ersetzt. Beim Anzeigen öffnete Windows dann eine SMB-Verbindung zum
    /// fremden Rechner und schickte den NTLM-Anmeldehash mit.
    /// </para>
    /// <para>
    /// <b>Zweimal geprüft:</b> der Server weist solche Zeilen ab (<c>Invalid</c>), und der Client
    /// leert solche Felder beim Abholen - falls ein Server sie doch liefert.
    /// </para>
    /// </summary>
    public static class SyncDateifelder
    {
        /// <summary>Je Zeilentyp die Dateifelder und wie sie als Medienpfad aussehen.</summary>
        private static readonly Dictionary<string, (string Feld, Func<string, string?> AlsPfad)[]> Felder = new(StringComparer.Ordinal)
        {
            [nameof(QuestionStepResource)] = [(nameof(QuestionStepResource.ResourceFileName), MediaPaths.ForResource)],
            [nameof(RevealQuestion)] = [(nameof(RevealQuestion.ImageFileName), MediaPaths.ForResource)],
            [nameof(Player)] = [(nameof(Player.UserPictureFileName), name => name)],

            // Ein Ordnername ist kein Dateipfad - geprueft wird er ueber eine Probedatei darin.
            [nameof(GameTheme)] = [(nameof(GameTheme.FolderName), ordner => MediaPaths.ForTheme(ordner, "probe.png"))],
        };

        /// <summary>
        /// Beschreibt das erste unzulässige Dateifeld der Zeile - oder <c>null</c>, wenn alles passt.
        /// Leere Felder sind immer zulässig.
        /// </summary>
        public static string? Pruefe(SyncRow row)
        {
            ArgumentNullException.ThrowIfNull(row);

            foreach (var feld in UnzulaessigeFelder(row))
                return $"'{feld}' ist kein zulässiger Dateiname (nur ein Name im Datenordner, kein Pfad).";

            return null;
        }

        /// <summary>
        /// Leert die unzulässigen Dateifelder einer Zeile. Gibt zurück, ob etwas geleert wurde.
        /// </summary>
        public static bool Entschaerfe(SyncRow row)
        {
            ArgumentNullException.ThrowIfNull(row);

            var felder = UnzulaessigeFelder(row).ToList();

            foreach (var feld in felder)
                row.Values[feld] = JsonSerializer.SerializeToElement(string.Empty);

            return felder.Count > 0;
        }

        private static IEnumerable<string> UnzulaessigeFelder(SyncRow row)
        {
            if (!Felder.TryGetValue(row.Type, out var felder))
                yield break;

            foreach (var (feld, alsPfad) in felder)
            {
                if (!row.Values.TryGetValue(feld, out var wert) || wert.ValueKind != JsonValueKind.String)
                    continue;

                var text = wert.GetString();

                if (string.IsNullOrEmpty(text))
                    continue;

                // Genau ein Name - kein Trenner, kein Laufwerk. TryNormalize allein liesse bei
                // den Spielerbildern "Resources/x.png" durch, das waere zwar harmlos, aber nicht
                // das, was dort steht.
                var einName = text.IndexOfAny(['/', '\\', ':']) < 0;

                if (!einName || alsPfad(text) is not { } pfad || !MediaPaths.TryNormalize(pfad, out _))
                    yield return feld;
            }
        }
    }
}
