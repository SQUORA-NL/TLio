using System.Buffers;
using System.Text;

namespace TLio.Core.Internal;

/// <summary>
/// A <see cref="TextWriter"/> that collects text in one rented buffer and makes exactly one
/// string out of it.
/// </summary>
/// <remarks>
/// A <see cref="StringBuilder"/> grows by adding chunks and then copies them all into the final
/// string, so a 700 KB document is held twice for the length of the conversion — once as
/// chunks that outlive the next collection, once as the string. This holds it once, in an array
/// the pool takes back, and the string is the only large allocation that is left.
/// </remarks>
internal sealed class PooledStringWriter : TextWriter
{
    private char[] _buffer = ArrayPool<char>.Shared.Rent(8 * 1024);
    private int _length;

    public override Encoding Encoding => Encoding.Unicode;

    public override string ToString() => new(_buffer, 0, _length);

    public override void Write(char value)
    {
        if (_length == _buffer.Length) Grow(1);
        _buffer[_length++] = value;
    }

    public override void Write(char[] buffer, int index, int count) =>
        Write(new ReadOnlySpan<char>(buffer, index, count));

    public override void Write(ReadOnlySpan<char> buffer)
    {
        if (_length + buffer.Length > _buffer.Length) Grow(buffer.Length);
        buffer.CopyTo(_buffer.AsSpan(_length));
        _length += buffer.Length;
    }

    public override void Write(string? value)
    {
        if (value is not null) Write(value.AsSpan());
    }

    private void Grow(int atLeast)
    {
        var next = ArrayPool<char>.Shared.Rent(Math.Max(_buffer.Length * 2, _length + atLeast));
        _buffer.AsSpan(0, _length).CopyTo(next);
        ArrayPool<char>.Shared.Return(_buffer);
        _buffer = next;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _buffer.Length > 0)
        {
            ArrayPool<char>.Shared.Return(_buffer);
            _buffer = [];
            _length = 0;
        }

        base.Dispose(disposing);
    }
}
