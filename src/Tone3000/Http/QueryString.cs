using System.Globalization;
using System.Text;
using Tone3000.Models;
using Tone3000.Serialization;

namespace Tone3000.Http;

internal sealed class QueryString
{
    private readonly StringBuilder _sb = new();

    public QueryString Add(string name, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return this;
        }

        _sb.Append(_sb.Length == 0 ? '?' : '&')
           .Append(Uri.EscapeDataString(name))
           .Append('=')
           .Append(Uri.EscapeDataString(value));
        return this;
    }

    public QueryString Add(string name, long? value)
        => Add(name, value?.ToString(CultureInfo.InvariantCulture));

    public QueryString Add(string name, bool? value)
        => Add(name, value is null ? null : value.Value ? "true" : "false");

    public QueryString AddEnum<TEnum>(string name, TEnum? value)
        where TEnum : struct, Enum
        => value is null || EqualityComparer<TEnum>.Default.Equals(value.Value, default)
            ? this
            : Add(name, WireValues.ToWire(value.Value));

    public QueryString AddJoined(string name, IEnumerable<string>? values, char separator)
    {
        if (values is null)
        {
            return this;
        }

        var list = values.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        foreach (var v in list)
        {
            if (v.Contains(separator, StringComparison.Ordinal))
            {
                throw new ArgumentException($"'{name}' values cannot contain '{separator}', the API's separator: \"{v}\".", name);
            }
        }

        return list.Count == 0 ? this : Add(name, string.Join(separator, list));
    }

    public QueryString AddPaging(PagedQuery? query)
        => query is null ? this : Add("page", query.Page).Add("page_size", query.PageSize);

    public override string ToString() => _sb.ToString();
}
