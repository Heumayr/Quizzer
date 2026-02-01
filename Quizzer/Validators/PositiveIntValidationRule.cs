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
                return new ValidationResult(false, "Required");

            if (!int.TryParse(text, out var n))
                return new ValidationResult(false, "Numbers only");

            if (n < 0)
                return new ValidationResult(false, "Must be >= 0");

            return ValidationResult.ValidResult;
        }
    }
}