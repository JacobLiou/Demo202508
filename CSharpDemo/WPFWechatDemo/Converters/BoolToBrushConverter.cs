using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WPFWechatDemo
{
    public class BoolToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isSent)
            {
                // 发送的消息用绿色，接收的消息用白色
                return isSent ? new SolidColorBrush(Color.FromRgb(155, 220, 95)) : Brushes.White;
            }
            return Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

