namespace IconWorkshop.Packs;

/// <summary>
/// Minimal reader for resources.arsc: maps resource names to their files (with screen density)
/// and resolves string resources (e.g. the app name). Needed for APKs whose resource files are
/// renamed/obfuscated (res/a1.png instead of res/drawable-nodpi/chrome.png).
/// </summary>
internal sealed class ArscTable
{
    public sealed record FileValue(string Path, int Density);

    private readonly Dictionary<string, List<FileValue>> _files = new(StringComparer.Ordinal); // "type/name"
    private readonly Dictionary<uint, (string Type, string Name)> _names = new();
    private readonly Dictionary<uint, string> _defaultStrings = new();
    private readonly Dictionary<uint, string> _otherStrings = new();

    public static ArscTable? TryParse(byte[] data)
    {
        try
        {
            var t = new ArscTable();
            t.Parse(data);
            return t;
        }
        catch { return null; }
    }

    /// <summary>All files of a resource ("drawable", "mipmap", "xml" ...).</summary>
    public IReadOnlyList<FileValue> Files(string type, string name) =>
        _files.TryGetValue(type + "/" + name, out var list) ? list : Array.Empty<FileValue>();

    public IEnumerable<string> Names(string type)
    {
        string prefix = type + "/";
        foreach (var key in _files.Keys)
            if (key.StartsWith(prefix, StringComparison.Ordinal)) yield return key[prefix.Length..];
    }

    public (string Type, string Name)? NameOf(uint id) => _names.TryGetValue(id, out var n) ? n : null;

    public string? StringValue(uint id) =>
        _defaultStrings.TryGetValue(id, out var s) ? s : _otherStrings.TryGetValue(id, out var o) ? o : null;

    private void Parse(byte[] d)
    {
        if (BitConverter.ToUInt16(d, 0) != 0x0002) throw new InvalidDataException("no resource table");
        int headerSize = BitConverter.ToUInt16(d, 2);
        int end = Math.Min(d.Length, BitConverter.ToInt32(d, 4));
        ResStringPool? global = null;
        int p = headerSize;
        while (p + 8 <= end)
        {
            int type = BitConverter.ToUInt16(d, p);
            int size = BitConverter.ToInt32(d, p + 4);
            if (size <= 0) break;
            if (type == 0x0001 && global is null) global = new ResStringPool(d, p);
            else if (type == 0x0200 && global is not null) ParsePackage(d, p, size, global);
            p += size;
        }
    }

    private void ParsePackage(byte[] d, int start, int size, ResStringPool global)
    {
        uint pkgId = BitConverter.ToUInt32(d, start + 8);
        int headerSize = BitConverter.ToUInt16(d, start + 2);
        int typeStringsOffset = BitConverter.ToInt32(d, start + 8 + 4 + 256);
        int keyStringsOffset = BitConverter.ToInt32(d, start + 8 + 4 + 256 + 8);
        var typeStrings = new ResStringPool(d, start + typeStringsOffset);
        var keyStrings = new ResStringPool(d, start + keyStringsOffset);

        int end = start + size;
        int p = start + headerSize;
        while (p + 8 <= end)
        {
            int type = BitConverter.ToUInt16(d, p);
            int chunkSize = BitConverter.ToInt32(d, p + 4);
            if (chunkSize <= 0) break;
            if (type == 0x0201) ParseType(d, p, pkgId, typeStrings, keyStrings, global);
            p += chunkSize;
        }
    }

    private void ParseType(byte[] d, int start, uint pkgId, ResStringPool typeStrings, ResStringPool keyStrings, ResStringPool global)
    {
        int headerSize = BitConverter.ToUInt16(d, start + 2);
        int typeId = d[start + 8];
        int flags = d[start + 9];
        int entryCount = BitConverter.ToInt32(d, start + 12);
        int entriesStart = start + BitConverter.ToInt32(d, start + 16);
        int config = start + 20;
        int configSize = BitConverter.ToInt32(d, config);
        int density = configSize >= 16 ? BitConverter.ToUInt16(d, config + 14) : 0;
        bool defaultLocale = configSize < 12 || (d[config + 8] == 0 && d[config + 9] == 0);
        string typeName = typeStrings.Get(typeId - 1) ?? "";

        bool sparse = (flags & 0x01) != 0;
        bool offset16 = (flags & 0x02) != 0;
        int offsets = start + headerSize;

        for (int i = 0; i < entryCount; i++)
        {
            int index, offset;
            if (sparse)
            {
                index = BitConverter.ToUInt16(d, offsets + i * 4);
                offset = BitConverter.ToUInt16(d, offsets + i * 4 + 2) * 4;
            }
            else if (offset16)
            {
                index = i;
                int v = BitConverter.ToUInt16(d, offsets + i * 2);
                if (v == 0xFFFF) continue;
                offset = v * 4;
            }
            else
            {
                index = i;
                uint v = BitConverter.ToUInt32(d, offsets + i * 4);
                if (v == 0xFFFFFFFF) continue;
                offset = (int)v;
            }

            int e = entriesStart + offset;
            if (e + 8 > d.Length) continue;
            int entryFlags = BitConverter.ToUInt16(d, e + 2);
            uint key;
            int dataType;
            uint data;
            if ((entryFlags & 0x0008) != 0) // compact entry
            {
                key = BitConverter.ToUInt16(d, e);
                dataType = entryFlags >> 8;
                data = BitConverter.ToUInt32(d, e + 4);
            }
            else
            {
                key = BitConverter.ToUInt32(d, e + 4);
                if ((entryFlags & 0x0001) != 0) // complex (map) entry: not needed
                {
                    RememberName(pkgId, typeId, index, typeName, keyStrings.Get(key));
                    continue;
                }
                int entrySize = BitConverter.ToUInt16(d, e);
                int v = e + entrySize;
                if (v + 8 > d.Length) continue;
                dataType = d[v + 3];
                data = BitConverter.ToUInt32(d, v + 4);
            }

            string? name = keyStrings.Get(key);
            if (name is null) continue;
            uint id = RememberName(pkgId, typeId, index, typeName, name);

            if (dataType == 0x03) // string
            {
                string? value = global.Get(data);
                if (value is null) continue;
                if (value.StartsWith("res/", StringComparison.Ordinal) || value.StartsWith("r/", StringComparison.Ordinal))
                {
                    string k = typeName + "/" + name;
                    if (!_files.TryGetValue(k, out var list)) _files[k] = list = new();
                    list.Add(new FileValue(value, density));
                }
                else if (typeName == "string")
                {
                    if (defaultLocale) _defaultStrings[id] = value;
                    else _otherStrings.TryAdd(id, value); // fallback if there is no default value
                }
            }
        }
    }

    private uint RememberName(uint pkgId, int typeId, int index, string typeName, string? name)
    {
        uint id = (pkgId << 24) | ((uint)typeId << 16) | (uint)index;
        if (name is not null) _names[id] = (typeName, name);
        return id;
    }
}
