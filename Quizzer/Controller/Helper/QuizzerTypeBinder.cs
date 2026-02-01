using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.Controller.Helper
{
    public class QuizzerTypeBinder : ISerializationBinder
    {
        private static readonly HashSet<Type> Allowed = new()
        {
            typeof(Quizzer.Datamodels.QuestionTypes.DefaultQuestion),
            // add more question types here...
        };

        public Type BindToType(string? assemblyName, string typeName)
        {
            var t = Type.GetType($"{typeName}, {assemblyName}", throwOnError: false);
            if (t == null || !Allowed.Contains(t))
                throw new Newtonsoft.Json.JsonSerializationException($"Type not allowed: {typeName}");
            return t;
        }

        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
        {
            assemblyName = serializedType.Assembly.FullName;
            typeName = serializedType.FullName;
        }
    }
}