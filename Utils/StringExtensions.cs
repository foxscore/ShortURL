namespace ShortURL.Utils;

public static class StringExtensions
{
    public static bool ToBoolean(this string? value)
    {
        var adapted = value?.Trim().ToLower();
        return adapted is "true" or "1" or "yes" or "y" or "on" or "enabled";
    }
}
