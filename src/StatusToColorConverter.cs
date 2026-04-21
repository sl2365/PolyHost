using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ElementalTracker
{
    public class StatusToColorConverter : IValueConverter
    {
        // These are set by the theme system and used by all instances
        public static Brush OkBrush = new SolidColorBrush(Color.FromRgb(0, 150, 0));
        public static Brush ChangedBrush = new SolidColorBrush(Color.FromRgb(200, 140, 0));
        public static Brush ErrorBrush = new SolidColorBrush(Color.FromRgb(220, 40, 40));
        public static Brush DefaultBrush = new SolidColorBrush(Color.FromRgb(0, 0, 0));

        public static void UpdateFromTheme(ThemeSettings theme)
        {
            OkBrush = new SolidColorBrush(theme.CellStatusOk);
            ChangedBrush = new SolidColorBrush(theme.CellStatusChanged);
            ErrorBrush = new SolidColorBrush(theme.CellStatusError);
            DefaultBrush = new SolidColorBrush(theme.CellStatusDefault);
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value as string ?? "";
            switch (status)
            {
                case "ok": return OkBrush;
                case "changed": return ChangedBrush;
                case "error": return ErrorBrush;
                default: return DefaultBrush;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}