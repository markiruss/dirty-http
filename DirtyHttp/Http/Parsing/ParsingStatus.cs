namespace DirtyHttp.Http.Parsing;

internal enum ParsingStatus
{    
    FirstLine,
    Headers,
    Body,
    Complete,
    Error
}
