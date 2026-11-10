using System.Text;
using DirtyHttp.Http;
using DirtyHttp.Http.Parsing;

namespace DirtyHttp.Tests.Support;

/// <summary>
/// A copy of what ParseChunk returned. ParseResponse is a ref struct, so it can't be stored or
/// returned from a lambda; this snapshot can.
/// </summary>
internal sealed record ParseResult(ParsingStatus Status, DirtyHttpRequest Request, byte[] LeftOver)
{
    public string LeftOverText => Encoding.Latin1.GetString(LeftOver);
}

internal static class ParserHarness
{
    public static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    /// <summary>Feeds the whole request to a fresh parser in a single call.</summary>
    public static ParseResult ParseOnce(string raw) => ParseOnce(Bytes(raw));

    public static ParseResult ParseOnce(byte[] raw)
    {
        var parser = new HttpParser();
        var response = parser.ParseChunk(raw);
        return new ParseResult(response.Status, response.Request, response.LeftOver.ToArray());
    }

    /// <summary>
    /// Feeds the data to a fresh parser as a series of network reads, the same way TcpHttpReader does:
    /// whatever the parser hands back as LeftOver is put in front of the next chunk.
    /// Stops at the first Complete/Error; any chunks not yet fed are appended to LeftOver.
    /// </summary>
    public static ParseResult ParseInChunks(params byte[][] chunks)
    {
        var parser = new HttpParser();
        byte[] pending = [];
        ParseResult? last = null;

        for (int i = 0; i < chunks.Length; i++)
        {
            byte[] buffer = [.. pending, .. chunks[i]];
            var response = parser.ParseChunk(buffer);
            last = new ParseResult(response.Status, response.Request, response.LeftOver.ToArray());

            if (last.Status is ParsingStatus.Complete or ParsingStatus.Error)
            {
                byte[] rest = [.. last.LeftOver, .. chunks.Skip(i + 1).SelectMany(c => c)];
                return last with { LeftOver = rest };
            }

            pending = last.LeftOver;
        }

        return last ?? throw new ArgumentException("No chunks supplied", nameof(chunks));
    }

    /// <summary>Splits the data into two chunks at <paramref name="splitAt"/> and parses them.</summary>
    public static ParseResult ParseSplitAt(byte[] raw, int splitAt) =>
        ParseInChunks(raw[..splitAt], raw[splitAt..]);
}
