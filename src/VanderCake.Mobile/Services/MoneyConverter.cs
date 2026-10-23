using System.Globalization;
namespace VanderCake.Mobile.Services;
public sealed class MoneyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is long cents ? (cents / 100m).ToString("C2", CultureInfo.GetCultureInfo("pt-BR")) : "—";
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
