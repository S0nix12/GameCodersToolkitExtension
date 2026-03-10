using System;
using System.Globalization;
using System.Windows.Data;

namespace GameCodersToolkitShared.FileRenamer.Converters
{
	/// <summary>
	/// Converts any object to bool: true if non-null, false if null.
	/// Useful for enabling controls when a selection has been made.
	/// </summary>
	[ValueConversion(typeof(object), typeof(bool))]
	public class NotNullToBoolConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value != null;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
