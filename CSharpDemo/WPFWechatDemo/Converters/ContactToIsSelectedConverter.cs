using System;
using System.Globalization;
using System.Windows.Data;
using WPFWechatDemo.Models;

namespace WPFWechatDemo
{
    public class ContactToIsSelectedConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is Contact currentContact && values[1] is Contact selectedContact)
            {
                return currentContact?.Id == selectedContact?.Id;
            }
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

