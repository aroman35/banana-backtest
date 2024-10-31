using Banana.Backtest.Common.Models.Root;
using Newtonsoft.Json;

namespace Banana.Backtest.CryptoConverter.Converters;

public class SymbolNewtonsoftJsonConverter : JsonConverter<Symbol>
{
    public static SymbolNewtonsoftJsonConverter Instance = new();

    private SymbolNewtonsoftJsonConverter()
    {
    }

    public override void WriteJson(JsonWriter writer, Symbol value, JsonSerializer serializer)
    {
        writer.WriteValue(value.ToString());
    }

    public override Symbol ReadJson(
        JsonReader reader,
        Type objectType,
        Symbol existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(reader.Value);
        return Symbol.Parse((string)reader.Value);
    }
}
