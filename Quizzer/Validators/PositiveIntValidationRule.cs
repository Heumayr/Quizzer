using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Controls;

namespace Quizzer.Validators
{
    public class PositiveIntValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            var text = (value ?? "").ToString()?.Trim() ?? "";

            if (string.IsNullOrEmpty(text))
                return new ValidationResult(false, "Das Feld darf nicht leer sein.");

            if (!int.TryParse(text, out var n))
                return new ValidationResult(false, "Hier gehört eine ganze Zahl hinein.");

            if (n < 0)
                return new ValidationResult(false, "Die Zahl darf nicht negativ sein.");

            return ValidationResult.ValidResult;
        }
    }
}