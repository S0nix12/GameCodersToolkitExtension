using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;
using System.Windows.Media;

namespace GameCodersToolkitShared.FileRenamer.Converters
{
	[ValueConversion(typeof(bool), typeof(Brush))]
	public class BoolToResultColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is bool isSuccess)
			{
				return isSuccess
					? new SolidColorBrush(Colors.LightGreen)
					: new SolidColorBrush(Colors.Salmon);
			}
			return new SolidColorBrush(Colors.White);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
