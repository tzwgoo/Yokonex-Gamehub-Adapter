using System.Reflection;
using System.Globalization;

namespace STS2Bridge.Runtime;

internal static class RuntimeReflectionHelpers
{
    private const BindingFlags DefaultFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static bool TryGetString(object? instance, IReadOnlyList<string> memberNames, out string value)
    {
        value = string.Empty;
        if (instance is null)
        {
            return false;
        }

        foreach (var memberName in memberNames)
        {
            var candidate = GetMemberValue(instance, memberName);
            if (candidate is string text && !string.IsNullOrWhiteSpace(text))
            {
                value = text;
                return true;
            }
        }

        return false;
    }

    public static bool TryGetDisplayString(object? instance, IReadOnlyList<string> memberNames, out string value)
    {
        value = string.Empty;
        if (instance is null)
        {
            return false;
        }

        foreach (var memberName in memberNames)
        {
            var candidate = GetMemberValue(instance, memberName);
            if (TryConvertDisplayString(candidate, out var text))
            {
                value = text;
                return true;
            }
        }

        return false;
    }

    public static bool TryGetIdentifierString(object? instance, IReadOnlyList<string> memberNames, out string value)
    {
        value = string.Empty;
        if (instance is null)
        {
            return false;
        }

        foreach (var memberName in memberNames)
        {
            var candidate = GetMemberValue(instance, memberName);
            if (TryConvertToString(candidate, out var text))
            {
                value = text;
                return true;
            }
        }

        return false;
    }

    public static bool TryGetInt(object? instance, IReadOnlyList<string> memberNames, out int value)
    {
        value = default;
        if (instance is null)
        {
            return false;
        }

        foreach (var memberName in memberNames)
        {
            var candidate = GetMemberValue(instance, memberName);
            if (TryConvertToInt(candidate, out var number))
            {
                value = number;
                return true;
            }
        }

        return false;
    }

    public static bool TryConvertToInt(object? candidate, out int value)
    {
        value = default;
        if (candidate is null)
        {
            return false;
        }

        try
        {
            switch (candidate)
            {
                case int number:
                    value = number;
                    return true;
                case byte number:
                    value = number;
                    return true;
                case sbyte number:
                    value = number;
                    return true;
                case short number:
                    value = number;
                    return true;
                case ushort number:
                    value = number;
                    return true;
                case uint number:
                    value = checked((int)number);
                    return true;
                case long number:
                    value = checked((int)number);
                    return true;
                case ulong number:
                    value = checked((int)number);
                    return true;
                case decimal number:
                    value = decimal.ToInt32(number);
                    return true;
                case float number when number % 1 == 0:
                    value = checked((int)number);
                    return true;
                case double number when number % 1 == 0:
                    value = checked((int)number);
                    return true;
                default:
                    value = Convert.ToInt32(candidate);
                    return true;
            }
        }
        catch
        {
            value = default;
            return false;
        }
    }

    public static bool TryConvertToString(object? candidate, out string value)
    {
        value = string.Empty;
        if (candidate is null)
        {
            return false;
        }

        switch (candidate)
        {
            case string text when !string.IsNullOrWhiteSpace(text):
                value = text;
                return true;
            case IFormattable formattable:
            {
                var formatted = formattable.ToString(null, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(formatted))
                {
                    value = formatted;
                    return true;
                }

                return false;
            }
            default:
            {
                var converted = Convert.ToString(candidate, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(converted))
                {
                    value = converted;
                    return true;
                }

                return false;
            }
        }
    }

    public static bool TryConvertDisplayString(object? candidate, out string value)
    {
        value = string.Empty;
        if (candidate is null)
        {
            return false;
        }

        if (candidate is string text)
        {
            value = text;
            return !string.IsNullOrWhiteSpace(value);
        }

        var formattedTextMethod = candidate.GetType().GetMethod("GetFormattedText", Type.EmptyTypes);
        if (formattedTextMethod is not null && formattedTextMethod.ReturnType == typeof(string))
        {
            try
            {
                if (formattedTextMethod.Invoke(candidate, null) is string formatted && !string.IsNullOrWhiteSpace(formatted))
                {
                    value = formatted;
                    return true;
                }
            }
            catch
            {
            }
        }

        return TryConvertToString(candidate, out value);
    }

    public static bool TryGetBool(object? instance, IReadOnlyList<string> memberNames, out bool value)
    {
        value = default;
        if (instance is null)
        {
            return false;
        }

        foreach (var memberName in memberNames)
        {
            var candidate = GetMemberValue(instance, memberName);
            if (candidate is bool flag)
            {
                value = flag;
                return true;
            }
        }

        return false;
    }

    public static object? GetMemberValue(object? instance, string memberName)
    {
        if (instance is null)
        {
            return null;
        }

        var type = instance.GetType();
        var property = type.GetProperty(memberName, DefaultFlags);
        if (property is not null)
        {
            return property.GetValue(instance);
        }

        var field = type.GetField(memberName, DefaultFlags);
        if (field is not null)
        {
            return field.GetValue(instance);
        }

        return null;
    }
}
