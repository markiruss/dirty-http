namespace DirtyHttp.Http.Parsing;

internal ref struct ParseResponse
{
    public ParsingStatus Status { get; set; }
    public DirtyHttpRequest Request { get; set; }
    public Span<byte> LeftOver { get; set; }
}
