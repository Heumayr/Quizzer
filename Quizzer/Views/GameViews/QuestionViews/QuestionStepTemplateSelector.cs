using Quizzer.DataModels.Enumerations;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Quizzer.Views.GameViews.QuestionViews
{
    public class QuestionStepTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (container is not FrameworkElement fe)
                return base.SelectTemplate(item, container);

            if (item is not QuestionStepViewContext ctx)
                return fe.FindResource("DefaultStepTemplate") as DataTemplate;

            return ctx.QuestionType switch
            {
                QuestionType.MultipleChoice => fe.FindResource("MultipleChoiceStepTemplate") as DataTemplate,
                QuestionType.Properties => fe.FindResource("PropertiesStepTemplate") as DataTemplate,
                QuestionType.Appreciate => fe.FindResource("AppreciateStepTemplate") as DataTemplate,
                _ => fe.FindResource("DefaultStepTemplate") as DataTemplate
            };
        }
    }
}