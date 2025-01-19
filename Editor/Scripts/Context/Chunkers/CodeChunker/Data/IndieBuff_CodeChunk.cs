public class IndieBuff_CodeChunk
{   
    public string chunk;
    public int startLine;
    public int endLine;
    public string filePath;

    public IndieBuff_CodeChunk(string chunk, int startLine, int endLine, string filePath)
    {
        this.chunk = chunk;
        this.startLine = startLine;
        this.endLine = endLine;
        this.filePath = filePath;
    }
}