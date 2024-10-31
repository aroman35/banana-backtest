using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;

namespace Banana.Backtest.CryptoConverter.Extensions;

public static class EnumExtensions
{
    // Cache for Enum Value to Description
    private static readonly ConcurrentDictionary<Enum, string> EnumToDescriptionCache = new();

    // Cache for Description to Enum Value
    private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, Enum>> DescriptionToEnumCache = new();

    // Retrieve the description of an enum value with caching
    public static string GetDescription(this Enum enumValue)
    {
        // Try to get from cache first
        if (EnumToDescriptionCache.TryGetValue(enumValue, out var cachedDescription))
        {
            return cachedDescription;
        }

        // If not in cache, compute and cache the result
        var field = enumValue.GetType().GetField(enumValue.ToString());
        var description = enumValue.ToString(); // Default to the enum name if no description found

        if (field != null)
        {
            var attribute = field.GetCustomAttribute<DescriptionAttribute>();
            if (attribute != null)
            {
                description = attribute.Description;
            }
        }

        // Cache the description
        EnumToDescriptionCache.TryAdd(enumValue, description);

        return description;
    }

    // Retrieve the enum value from the description with caching
    public static T GetEnumByDescription<T>(this string description)
        where T : Enum
    {
        var enumType = typeof(T);

        // Ensure cache exists for this enum type
        if (!DescriptionToEnumCache.TryGetValue(enumType, out var descriptionEnumMap))
        {
            descriptionEnumMap = new ConcurrentDictionary<string, Enum>();
            DescriptionToEnumCache.TryAdd(enumType, descriptionEnumMap);
        }

        // Try to get from cache first
        if (descriptionEnumMap.TryGetValue(description, out var cachedEnumValue))
        {
            return (T)cachedEnumValue;
        }

        // If not in cache, compute the enum value
        foreach (var field in enumType.GetFields())
        {
            var attribute = field.GetCustomAttribute<DescriptionAttribute>();
            var fieldDescription = attribute != null ? attribute.Description : field.Name;

            if (fieldDescription == description)
            {
                var enumValue = (Enum)field.GetValue(null)!;

                // Cache the description-to-enum value mapping
                descriptionEnumMap.TryAdd(description, enumValue);

                return (T)enumValue;
            }
        }

        // If no match, throw exception
        throw new ArgumentException($"No enum with description '{description}' found in {enumType}.");
    }
}
