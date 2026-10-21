using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Quizzer.DataModels.Transfer.Sync;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Quizzer.Logic.Sync
{
    /// <summary>
    /// Wandelt Entitaeten in <see cref="SyncRow"/>s und zurueck - <b>ueber die EF-Metadaten</b>,
    /// nicht ueber eine Liste je Typ. Mitgenommen wird genau, was EF als Spalte kennt, ohne
    /// Schluessel, Zeilenversion und die gesperrten Felder aus <see cref="SyncModel"/>. Kommt
    /// eine Spalte dazu, wandert sie ohne Zutun mit; wer eine sperren will, traegt sie dort ein.
    /// </summary>
    public static class SyncRowConverter
    {
        public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        /// <summary>Die Spalten eines Typs, die im Abgleich stehen, nach Namen geordnet.</summary>
        internal static IReadOnlyList<IProperty> SyncedProperties(IModel model, Type clrType)
        {
            var entityType = model.FindEntityType(clrType)
                ?? throw new SyncFormatException($"Der Typ '{clrType.Name}' ist kein Teil des Datenmodells.");

            return entityType.GetProperties()
                .Where(p => !p.IsPrimaryKey())
                .Where(p => !p.IsConcurrencyToken)
                .Where(p => !p.IsShadowProperty())
                .Where(p => !SyncModel.IsExcluded(clrType, p.Name))
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .ToList();
        }

        internal static SyncRow ToRow(IModel model, object entity)
        {
            var clrType = entity.GetType();
            var entry = SyncModel.For(clrType);

            var idProperty = model.FindEntityType(clrType)!.FindPrimaryKey()!.Properties.Single();
            var id = (Guid)idProperty.GetGetter().GetClrValue(entity)!;

            var row = new SyncRow { Type = entry.Name, Id = id };

            foreach (var property in SyncedProperties(model, clrType))
            {
                var value = property.GetGetter().GetClrValue(entity);
                row.Values[property.Name] = JsonSerializer.SerializeToElement(value, property.ClrType, JsonOptions);
            }

            row.Hash = ComputeHash(row);
            return row;
        }

        /// <summary>
        /// Schreibt die Werte einer Zeile in einen Eintrag. <paramref name="strict"/> weist
        /// unbekannte Felder ab (Server); der Client ueberliest sie, damit ein neuerer Server
        /// einen aelteren Client nicht zum Absturz bringt.
        /// </summary>
        internal static void ApplyValues(EntityEntry entry, SyncRow row, bool strict)
        {
            var properties = SyncedProperties(entry.Context.Model, entry.Entity.GetType())
                .ToDictionary(p => p.Name, StringComparer.Ordinal);

            foreach (var (name, element) in row.Values)
            {
                if (!properties.TryGetValue(name, out var property))
                {
                    if (strict)
                        throw new SyncFormatException($"'{row.Type}' hat kein Feld '{name}'.");

                    continue;
                }

                object? value;
                try
                {
                    value = element.Deserialize(property.ClrType, JsonOptions);
                }
                catch (JsonException ex)
                {
                    throw new SyncFormatException($"'{row.Type}.{name}' hat einen ungültigen Wert.", ex);
                }

                if (value == null && !property.IsNullable)
                    throw new SyncFormatException($"'{row.Type}.{name}' darf nicht leer sein.");

                var propertyEntry = entry.Property(name);

                if (!Equals(propertyEntry.CurrentValue, value))
                    propertyEntry.CurrentValue = value;
            }
        }

        /// <summary>
        /// SHA-256 ueber Typ, Id und alle Werte in Ordinal-Reihenfolge der Feldnamen. Die Werte
        /// gehen als ihr JSON-Text ein - beide Seiten erzeugen ihn mit <see cref="JsonOptions"/>.
        /// </summary>
        public static string ComputeHash(SyncRow row)
        {
            var text = new StringBuilder();
            text.Append(row.Type).Append('|').Append(row.Id.ToString("N"));

            foreach (var (name, value) in row.Values.OrderBy(v => v.Key, StringComparer.Ordinal))
            {
                text.Append('|').Append(name).Append('=').Append(value.GetRawText());
            }

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
        }
    }
}
