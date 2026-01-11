using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BpBinReader;

public class JsonWriter(TextWriter tw) {
    private readonly TextWriter m_Tw = tw;
    private int m_Indent;
    private bool m_NeedComma;

    public void BeginObject() {
        WriteCommaIfNeeded();
        m_Tw.Write("{");
        m_Indent++;
        m_NeedComma = false;
    }

    public void EndObject() {
        m_Indent--;
        m_Tw.Write("}");
        m_NeedComma = true;
    }

    public void BeginArray() {
        WriteCommaIfNeeded();
        m_Tw.Write("[");
        m_Indent++;
        m_NeedComma = false;
    }

    public void EndArray() {
        m_Indent--;
        m_Tw.Write("]");
        m_NeedComma = true;
    }

    public void WritePropertyName(string name) {
        if (m_NeedComma) {
            m_Tw.Write(",");
        }
        m_Tw.WriteLine();
        m_Tw.Write(new string(' ', (m_Indent + 2) * 2));
        WriteString(name);
        m_Tw.Write(": ");
        m_NeedComma = false;
    }

    public void WriteString(string? value) {
        WriteCommaIfNeeded();
        m_Tw.Write("\"");
        m_Tw.Write(Escape(value ?? string.Empty));
        m_Tw.Write("\"");
        m_NeedComma = true;
    }

    public void WriteNull() {
        WriteCommaIfNeeded();
        m_Tw.Write("null");
        m_NeedComma = true;
    }

    public void WriteBool(bool value) {
        WriteCommaIfNeeded();
        m_Tw.Write(value ? "true" : "false");
        m_NeedComma = true;
    }

    public void WriteNumber(int value) {
        WriteCommaIfNeeded();
        m_Tw.Write(value.ToString(CultureInfo.InvariantCulture));
        m_NeedComma = true;
    }

    public void WriteNumber(uint value) {
        WriteCommaIfNeeded();
        m_Tw.Write(value.ToString(CultureInfo.InvariantCulture));
        m_NeedComma = true;
    }

    public void WriteNumber(long value) {
        WriteCommaIfNeeded();
        m_Tw.Write(value.ToString(CultureInfo.InvariantCulture));
        m_NeedComma = true;
    }

    public void WriteNumber(ulong value) {
        WriteCommaIfNeeded();
        m_Tw.Write(value.ToString(CultureInfo.InvariantCulture));
        m_NeedComma = true;
    }

    public void WriteRawNumber(float value) {
        WriteCommaIfNeeded();
        m_Tw.Write(value.ToString("R", CultureInfo.InvariantCulture));
        m_NeedComma = true;
    }

    public void WriteRawNumber(double value) {
        WriteCommaIfNeeded();
        m_Tw.Write(value.ToString("R", CultureInfo.InvariantCulture));
        m_NeedComma = true;
    }

    private void WriteCommaIfNeeded() {
        // Arrays handle commas via _needComma between elements. Objects handle commas before properties.
        // For simplicity we keep one flag; property writer resets it.
        // If we're about to write a scalar/array/object element inside an array, it will work.
        if (m_NeedComma) {
            m_Tw.Write(", ");
            m_NeedComma = false;
        }
    }

    private static string Escape(string s) {
        return s
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }
}
