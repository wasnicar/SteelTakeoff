using System;
using System.Globalization;
using System.Windows.Data;

namespace SteelCoatingTakeoff.App
{
    /// <summary>
    /// Shows a numeric field as BLANK when it is zero (and reads a blank box back as zero),
    /// so a fresh row's Length doesn't display a "0" that reads like part of the takeoff.
    /// Non-zero values format with <see cref="Format"/> (default "0.##").
    /// </summary>
    public sealed class ZeroBlankConverter : IValueConverter
    {
        public string Format { get; set; } = "0.##";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "";
            var d = System.Convert.ToDouble(value, culture);
            return d == 0.0 ? "" : d.ToString(Format, culture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = value?.ToString();
            if (string.IsNullOrWhiteSpace(text)) return 0.0;
            return double.TryParse(text, NumberStyles.Any, culture, out var d) ? d : 0.0;
        }
    }
}
