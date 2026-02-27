using Quizzer.Views.BuzzerViews;
using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.Views.StaticRessources
{
    public static class StaticManager
    {
        public static BuzzerServerViewModel BuzzerServerViewModel { get; set; } = new();
    }
}