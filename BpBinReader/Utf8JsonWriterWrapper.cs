using System.Globalization;
using System.Text.Json;

namespace BpBinReader;

// Necessary for NaN /+- Infinity ._.
public class Utf8JsonWriterWrapper(Utf8JsonWriter writer) : IDisposable {
    private readonly Utf8JsonWriter m_Writer = writer;
    public void WriteStartObject() => m_Writer.WriteStartObject();
    public void WriteEndObject() => m_Writer.WriteEndObject();
    public void WriteNullValue() => m_Writer.WriteNullValue();
    public void WriteNumber(string propertyName, long value) => m_Writer.WriteNumber(propertyName, value);
    public void WriteNumber(string propertyName, int value) => m_Writer.WriteNumber(propertyName, value);
    public void WriteNumber(string propertyName, float value) {
        if (float.IsNaN(value) || float.IsInfinity(value)) {
            m_Writer.WriteString(propertyName, value.ToString("R", CultureInfo.InvariantCulture));
        } else {
            m_Writer.WriteNumber(propertyName, value);
        }
    }
    public void WriteNumber(string propertyName, double value) {
        if (double.IsNaN(value) || double.IsInfinity(value)) {
            m_Writer.WriteString(propertyName, value.ToString("R", CultureInfo.InvariantCulture));
        } else {
            m_Writer.WriteNumber(propertyName, value);
        }
    }
    public void WriteBoolean(string propertyName, bool value) => m_Writer.WriteBoolean(propertyName, value);
    public void WriteNumberValue(long value) => m_Writer.WriteNumberValue(value);
    public void WriteNumberValue(int value) => m_Writer.WriteNumberValue(value);
    public void WriteNumberValue(float value) {
        if (float.IsNaN(value) || float.IsInfinity(value)) {
            m_Writer.WriteStringValue(value.ToString("R", CultureInfo.InvariantCulture));
        } else {
            m_Writer.WriteNumberValue(value);
        }
    }
    public void WriteNumberValue(double value) {
        if (double.IsNaN(value) || double.IsInfinity(value)) {
            m_Writer.WriteStringValue(value.ToString("R", CultureInfo.InvariantCulture));
        } else {
            m_Writer.WriteNumberValue(value);
        }
    }
    public void WriteBooleanValue(bool value) => m_Writer.WriteBooleanValue(value);
    public void WriteStartArray() => m_Writer.WriteStartArray();
    public void WriteEndArray() => m_Writer.WriteEndArray();
    public void WriteStringValue(string? value) => m_Writer.WriteStringValue(value);
    public void WriteString(string propertyName, string? value) => m_Writer.WriteString(propertyName, value);
    public void WritePropertyName(string propertyName) => m_Writer.WritePropertyName(propertyName);
    public void Flush() => m_Writer.Flush();
    public void Dispose() {
        m_Writer.Dispose();
    }
}
