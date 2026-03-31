using Quizzer.DataModels.Enumerations;
using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.DataModels.Helpers
{
    public class Helper
    {
        public static string GetNextViewKey(string current, QuestionViewKeyType keyType)
        {
            switch (keyType)
            {
                case QuestionViewKeyType.Alphabetical:
                    return GetNextAlphabeticalEntry(current);

                case QuestionViewKeyType.Numerical:
                    return GetNextNumber(current);

                default:
                    throw new ArgumentOutOfRangeException(nameof(keyType), "Unsupported key type.");
            }
        }

        private static string GetNextNumber(string current)
        {
            if (string.IsNullOrWhiteSpace(current))
                return "1";
            if (int.TryParse(current, out int number))
                return (number + 1).ToString();
            throw new ArgumentException("Input must be a valid integer.", nameof(current));
        }

        private static string GetNextAlphabeticalEntry(string current)
        {
            if (string.IsNullOrWhiteSpace(current))
                return "A";

            current = current.Trim().ToUpperInvariant();

            char[] chars = current.ToCharArray();
            int index = chars.Length - 1;

            while (index >= 0)
            {
                if (chars[index] < 'A' || chars[index] > 'Z')
                    throw new ArgumentException("Input must contain only letters A-Z.", nameof(current));

                if (chars[index] == 'Z')
                {
                    chars[index] = 'A';
                    index--;
                }
                else
                {
                    chars[index]++;
                    return new string(chars);
                }
            }

            return "A" + new string(chars);
        }
    }
}