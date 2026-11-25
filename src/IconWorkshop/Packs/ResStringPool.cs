using System.Text;

namespace IconWorkshop.Packs;

/// <summary>Android ResStringPool (used by resources.arsc and binary XML).</summary>
internal sealed class ResStringPool
{
    private readonly byte[] _data;
    private readonly int[] _offsets;
    private readonly int _stringsStart;
    private readonly bool _utf8;
    private readonly string?[] _cache;

    public int Count => _offsets.Length;

    /// <param name="start">Offset of the chunk (type 0x0001) in <paramref name="data"/>.</param>
    public ResStringPool(byte[] data, int start)
    {
        _data = data;
        int headerSize = BitConverter.ToUInt16(data, start + 2);
        int count = BitConverter.ToInt32(data, start + 8);
        int flags = BitConverter.ToInt32(data, start + 16);
        _stringsStart = start + BitConverter.ToInt32(data, start + 20);
        _utf8 = (flags & 0x100) != 0;
        _offsets = new int[Math.Max(0, count)];
        int table = start + headerSize;
        for (int i = 0; i < _offsets.Length; i++) _offsets[i] = BitConverter.ToInt32(data, table + i * 4);
        _cache = new string?[_offsets.Length];
    }

    public string? Get(long index)
    {
        if (index < 0 || index >= _offsets.Length) return null;
        return _cache[index] ??= Decode(_stringsStart + _offsets[index]);
    }

    private string Decode(int p)
    {
        try
        {
            if (_utf8)
            {
                // UTF-16 length (skipped), then UTF-8 byte length; each 1 or 2 bytes.
                if ((_data[p] & 0x80) != 0) p += 2; else p += 1;
                int len = _data[p];
                if ((len & 0x80) != 0) { len = ((len & 0x7F) << 8) | _data[p + 1]; p += 2; } else p += 1;
                return Encoding.UTF8.GetString(_data, p, len);
            }
            else
            {
                int len = BitConverter.ToUInt16(_data, p);
                if ((len & 0x8000) != 0) { len = ((len & 0x7FFF) << 16) | BitConverter.ToUInt16(_data, p + 2); p += 4; } else p += 2;
                return Encoding.Unicode.GetString(_data, p, len * 2);
            }
        }
        catch { return ""; }
    }
}
