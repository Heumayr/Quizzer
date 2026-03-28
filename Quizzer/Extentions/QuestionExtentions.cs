using Quizzer.DataModels.Models.Base;
using System;
using System.Collections.Generic;
using System.Resources;
using System.Text;

namespace Quizzer.Extentions
{
    public static class QuestionExtentions
    {
        extension(List<QuestionStepResource> resources)
        {
            public Dictionary<string, string> GetKeyDictionary()
            {
                var keyDictionary = new Dictionary<string, string>();

                foreach (var item in resources)
                {
                    if (string.IsNullOrEmpty(item.QuestionViewKey) || keyDictionary.ContainsKey(item.QuestionViewKey))
                        continue;

                    var designation = string.IsNullOrEmpty(item.Designation) ? item.StepText : item.Designation;

                    keyDictionary[item.QuestionViewKey] = designation;
                }

                return keyDictionary;
            }
        }
    }
}