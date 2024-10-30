using System.Text.Json;
using System.Text.Json.Serialization;
using Banana.Backtest.Common.Models.Root;

namespace Banana.Backtest.CryptoConverter.Converters;

public class SymbolSystemTextJsonConverter : JsonConverter<Symbol>
{
    public static readonly SymbolSystemTextJsonConverter Instance = new();

    private SymbolSystemTextJsonConverter()
    {
    }
    
    public override Symbol Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        ArgumentNullException.ThrowIfNull(value);
        return Symbol.Parse(value);
    }

    public override void Write(Utf8JsonWriter writer, Symbol value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}