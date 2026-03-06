using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

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
}
