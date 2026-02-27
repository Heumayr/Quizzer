using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.DataModels.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ClearOnSaveAttribute : Attribute
    {
    }
}