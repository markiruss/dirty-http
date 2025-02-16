namespace DirtyHttp.Http;

public class DirtyHttpResponse
{
    public int StatusCode { get; set; }
    public string HttpVersion { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public string? Body { get; set; }
}
