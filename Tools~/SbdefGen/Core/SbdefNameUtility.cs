#nullable enable
using System;

namespace SbdefGen.Core;

internal static class SbdefNameUtility
{
    public static string ToPascal(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return char.ToUpper(value[0]) + value.Substring(1);
    }

    public static string ToCamel(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return char.ToLower(value[0]) + value.Substring(1);
    }

    public static string BuildListItemBaseName(string pascalName)
    {
        if (string.IsNullOrWhiteSpace(pascalName))
        {
            return pascalName;
        }

        if (pascalName.EndsWith("Entries", StringComparison.Ordinal))
        {
            return pascalName.Substring(0, pascalName.Length - "Entries".Length) + "Entry";
        }

        if (pascalName.EndsWith("ies", StringComparison.Ordinal) && pascalName.Length > 3)
        {
            return pascalName.Substring(0, pascalName.Length - 3) + "y";
        }

        if (pascalName.EndsWith("ses", StringComparison.Ordinal)
            || pascalName.EndsWith("xes", StringComparison.Ordinal)
            || pascalName.EndsWith("zes", StringComparison.Ordinal)
            || pascalName.EndsWith("ches", StringComparison.Ordinal)
            || pascalName.EndsWith("shes", StringComparison.Ordinal))
        {
            return pascalName.Substring(0, pascalName.Length - 2);
        }

        if (pascalName.EndsWith("s", StringComparison.Ordinal) && pascalName.Length > 1)
        {
            return pascalName.Substring(0, pascalName.Length - 1);
        }

        return pascalName;
    }

    public static string BuildListItemTypeName(string pascalName)
    {
        return BuildListItemBaseName(pascalName) + "Item";
    }
}
