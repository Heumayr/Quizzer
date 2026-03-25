using Quizzer.DataModels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Quizzer.Views.StaticRessources
{
    public static class StaticResources
    {
        public static readonly Brush CellImageBrush = new ImageBrush(
            new BitmapImage(new Uri(Settings.CellBackgroundImagePath, UriKind.Absolute)))
        {
            Stretch = Stretch.Fill
        };

        public static readonly Brush CellHoverImageBrush = new ImageBrush(
            new BitmapImage(new Uri(Settings.CellBackgroundHoverImagePath, UriKind.Absolute)))
        {
            Stretch = Stretch.Fill
        };

        public static readonly Brush CellImageBrushIsDone = new ImageBrush(
            new BitmapImage(new Uri(Settings.CellBackgroundIsDoneImagePath, UriKind.Absolute)))
        {
            Stretch = Stretch.Fill
        };

        public static readonly Brush HeaderColumnImageBrush = new ImageBrush(
            new BitmapImage(new Uri(Settings.HeaderColumnBackgroundImagePath, UriKind.Absolute)))
        {
            Stretch = Stretch.Fill
        };

        public static readonly Brush HeaderRowImageBrush = new ImageBrush(
            new BitmapImage(new Uri(Settings.HeaderRowBackgroundImagePath, UriKind.Absolute)))
        {
            Stretch = Stretch.Fill
        };

        public static readonly Brush PlayerPlaceHolderImageBrush = new ImageBrush(
            new BitmapImage(new Uri(Settings.PlaceholderPlayerImagePath, UriKind.Absolute)))
        {
            Stretch = Stretch.Fill
        };

        public static readonly Brush PlayerCardImageBrush = new ImageBrush(
            new BitmapImage(new Uri(Settings.PlayerCardBackgroundImagePath, UriKind.Absolute)))
        {
            Stretch = Stretch.Fill
        };

        public static readonly Brush PlayerCardWinnerImageBrush = new ImageBrush(
            new BitmapImage(new Uri(Settings.PlayerCardBackgroundWinnerImagePath, UriKind.Absolute)))
        {
            Stretch = Stretch.Fill
        };
    }
}