using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace BiaogeCSharp.Converters;

/// <summary>
/// 发送按钮文本转换器 - 根据发送状态显示不同文本
/// </summary>
public class SendButtonTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSending)
        {
            return isSending ? "发送中..." : "发送";
        }
        return "发送";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
