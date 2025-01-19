using System.Linq;

public class Span
{
    // Represents a slice of a string
    public int Start { get; set; } = 0;
    public int End { get; set; } = 0;

    public Span(int start = 0, int? end = null)
    {
        Start = start;
        End = end ?? start;
    }

    public string Extract(string s)
    {
        // Grab the corresponding substring of string s by bytes
        return s.Substring(Start, End - Start);
    }

    public string ExtractLines(string s)
    {
        // Split the string into an array of lines
        string[] lines = s.Split('\n');
        
        // Get the subset of lines from start to end index
        // and join them back together with newlines
        return string.Join("\n", lines.Skip(Start).Take(End - Start));
    }

    public static Span operator +(Span a, Span b)
    {
        // Span + Span behavior (concatenation)
        if (a.Length() == 0) return b;
        if (b.Length() == 0) return a;
        return new Span(a.Start, b.End);
    }

    public static Span operator +(Span a, int offset)
    {
        // Span + int behavior (shifting both start and end)
        return new Span(a.Start + offset, a.End + offset);
    }

    public int Length()
    {
        // i.e. Span(a, b) = b - a
        return End - Start;
    }
}