using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Controls;

namespace Quizzer.Validators
{
    public class PositiveDoubleValidationRule : ValidationRule
    {
        public bool AllowEmpty { get; set; } = false;

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            var text = (value ?? "").ToString()?.Trim() ?? "";

            if (string.IsNullOrEmpty(text))
                return AllowEmpty ? ValidationResult.ValidResult : new ValidationResult(false, "Das Feld darf nicht leer sein.");

            // Zwischenzustände erlauben, damit man tippen kann:
            // "0," "0." "," "."
            if (text == "," || text == "." || text.EndsWith(",") || text.EndsWith("."))
                text = text.TrimEnd(',', '.');

            // both separators / cultures
            bool ok =
                double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var n) ||
                double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out n);

            if (!ok)
                return new ValidationResult(false, "Hier gehört eine Zahl hinein – Nachkommastellen sind erlaubt.");

            if (n < 0.0)
                return new ValidationResult(false, "Die Zahl darf nicht negativ sein.");

            return ValidationResult.ValidResult;
        }
    }
}