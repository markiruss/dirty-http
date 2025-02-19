using System.Collections.Specialized;

namespace DirtyHttp.Http;

public class DirtyHttpRequest
{
    public HttpMethods Method { get; set; }    
    public string Path { get; set; } = string.Empty;
    public string HttpVersion { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } 
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
