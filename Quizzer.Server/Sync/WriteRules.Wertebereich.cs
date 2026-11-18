using Quizzer.DataModels.Transfer.Sync;
using Quizzer.Logic.Sync;
using System.Text.Json;

namespace Quizzer.Server.Sync
{
    /// <summary>Wertebereich der Enum-Felder (M37 P1, V21) - eigene Teildatei wie <c>WriteRules.Joker.cs</c>.</summary>
    internal sealed partial class WriteRules
    {
        /// <summary>
        /// Jedes Enum-Feld trägt einen bekannten Wert - der Abgleich nimmt Enums als Zahl, und eine fremde Sonderregel spielte
        /// das WPF als gewöhnliche Frage. Form wie die Joker-Art: über den Zusammenhang, also auch für den Admin. Die Felder
        /// kommen aus dem Datenmodell; Einzelwerte genügen, solange kein <c>[Flags]</c>-Enum im Abgleich steht (Test hält es).
        /// </summary>
        private string? CheckEnumRange(SyncRow row)
        {
            if (!SyncModel.TryGet(row.Type, out var entry))
                return null;

            foreach (var property in SyncRowConverter.SyncedProperties(context.Model, entry.ClrType))
            {
                var typ = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;

                if (!typ.IsEnum || !row.Values.TryGetValue(property.Name, out var wert) || wert.ValueKind != JsonValueKind.Number)
                    continue;

                if (!wert.TryGetInt64(out var zahl) || !Enum.IsDefined(typ, Enum.ToObject(typ, zahl)))
                    return $"Unbekannter Wert {wert.GetRawText()} für '{row.Type}.{property.Name}'.";
            }

            return null;
        }
    }
}
