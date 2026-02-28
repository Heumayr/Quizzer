using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text;

namespace Quizzer.Extentions
{
    public static class EnumExtentions
    {
        /// <summary>
        /// Always tries to return a [Description] attribute first.
        /// If none is found, falls back to ToString().
        /// Works for enums and any other object/type.
        /// </summary>
        public static string DescriptionOrString(this object? obj)
        {
            if (obj is null) return string.Empty;

            var type = obj.GetType();

            // 1) Enum value -> [Description] on the enum member
            if (type.IsEnum)
            {
                var name = Enum.GetName(type, obj);
                if (!string.IsNullOrEmpty(name))
                {
                    var field = type.GetField(name, BindingFlags.Public | BindingFlags.Static);
                    var desc = field?.GetCustomAttribute<DescriptionAttribute>()?.Description;
                    if (!string.IsNullOrWhiteSpace(desc))
                        return desc;
                }

                return obj.ToString() ?? string.Empty;
            }

            // 2) Non-enum -> [Description] on the type itself (class/struct)
            var typeDesc = type.GetCustomAttribute<DescriptionAttribute>()?.Description;
            if (!string.IsNullOrWhiteSpace(typeDesc))
                return typeDesc;

            // 4) Fallback
            return obj.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Always tries to return a [Description] attribute first.
        /// If none is found, falls back to ToString().
        /// Works for enums and any other object/type.
        /// </summary>
        public static string DisplayNameOrString(this object? obj)
        {
            if (obj is null) return string.Empty;

            var type = obj.GetType();

            if (type.IsEnum)
            {
                var name = Enum.GetName(type, obj);
                if (!string.IsNullOrEmpty(name))
                {
                    var field = type.GetField(name, BindingFlags.Public | BindingFlags.Static);
                    var desc = field?.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;
                    if (!string.IsNullOrWhiteSpace(desc))
                        return desc;
                }

                return obj.ToString() ?? string.Empty;
            }

            var typeDesc = type.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;
            if (!string.IsNullOrWhiteSpace(typeDesc))
                return typeDesc;

            return obj.ToString() ?? string.Empty;
        }
    }
}