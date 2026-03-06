using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace UnicornOverlord
{
	internal class NameIDConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			uint id = (uint)value;
			var nm = Info.Instance().Search(Info.Instance().Name, id);
			if (nm != null) return nm.Name;
			return id.ToString();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

	internal class GenericNameIDConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			uint id = (uint)value;
			var nm = Info.Instance().Search(Info.Instance().GenericName, id);
			if (nm != null) return nm.Name;
			return null!;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

	// Inverts a bool for IsEnabled bindings
	internal class BoolInverter : IValueConverter
	{
		public static readonly BoolInverter Instance = new BoolInverter();

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
			=> value is bool b ? !b : value;

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> value is bool b ? !b : value;
	}

	// Returns a greyed-out brush for locked slots, default foreground otherwise
	internal class LockedBrushConverter : IValueConverter
	{
		public static readonly LockedBrushConverter Instance = new LockedBrushConverter();

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
			=> value is bool isLocked && isLocked
				? new SolidColorBrush(Colors.Gray)
				: new SolidColorBrush(Colors.Black);

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotImplementedException();
	}
}
