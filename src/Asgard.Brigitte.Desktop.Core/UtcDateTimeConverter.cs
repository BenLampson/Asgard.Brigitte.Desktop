using System.Globalization;
using System.Text.Json.Serialization;
namespace Asgard.Brigitte.Desktop.Core;
/// <summary>兼容现有接口未携带 Z 的 UTC 时间。</summary>
public sealed class UtcDateTimeConverter : JsonConverter<DateTimeOffset>
{
    /// <summary>未声明偏移时按 UTC 处理。</summary>
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => DateTimeOffset.Parse(reader.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
    /// <summary>统一输出 UTC。</summary>
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToUniversalTime());
}
