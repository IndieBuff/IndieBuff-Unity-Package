using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System;
using System.IO;
using UnityEngine;

public class Span
{
    // Represents a slice of a string
    public int Start { get; set; } = 0;
    public int End { get; set; } = 0;

    public Span(int start = 0, int end = 0)
    {
        Start = start;
        End = end;
        
        // If end is null (not applicable in C#), set it to start
        if (end == 0)
        {
            End = start;
        }
    }

    public string Extract(string s)
    {
        // Grab the corresponding substring of string s by bytes
        return s.Substring(Start, End - Start);
    }

    public string ExtractLines(string s)
    {
        // Grab the corresponding substring of string s by lines
        return string.Join("\n", s.Split('\n').Skip(Start).Take(End - Start));
    }

    public static Span operator +(Span a, Span b)
    {
        // e.g. Span(1, 2) + Span(2, 4) = Span(1, 4) (concatenation)
        // There are no safety checks: Span(a, b) + Span(c, d) = Span(a, d)
        // and there are no requirements for b = c.
        return new Span(a.Start, b.End);
    }

    public static Span operator +(Span a, int b)
    {
        return new Span(a.Start + b, a.End + b);
    }

    public int Length()
    {
        // i.e. Span(a, b) = b - a
        return End - Start;
    }
}

public class CodeChunker
{
    private string PrettyNode(SyntaxNode node)
    {
        return $"{node.Kind()}:{node.Span.Start}-{node.Span.End}";
    }

    private void PrintTree(SyntaxNode node, string indent = "")
    {
        // Using Regex to remove whitespace, similar to Python's re.sub
        var nodeText = Regex.Replace(node.ToString(), @"\s", "");
        if (nodeText.Length < 100)
            return;
            
        Console.WriteLine(indent + PrettyNode(node));
        foreach (var child in node.ChildNodes())
        {
            PrintTree(child, indent + "  ");
        }
    }

    private List<Span> ConnectChunks(List<Span> chunks)
    {
        for (int i = 0; i < chunks.Count - 1; i++)
        {
            chunks[i].End = chunks[i + 1].Start;
        }
        return chunks;
    }

    private List<Span> ChunkNode(
        SyntaxNode node,
        int maxChars = 600)
    {
        var chunks = new List<Span>();
        var currentChunk = new Span(node.Span.Start, node.Span.Start);
        var nodeChildren = node.ChildNodes();
        
        foreach (var child in nodeChildren)
        {
            if (child.Span.Length > maxChars)
            {
                chunks.Add(currentChunk);
                currentChunk = new Span(child.Span.End, child.Span.End);
                chunks.AddRange(ChunkNode(child, maxChars));
            }
            else if (child.Span.Length + currentChunk.Length() > maxChars)
            {
                chunks.Add(currentChunk);
                currentChunk = new Span(child.Span.Start, child.Span.End);
            }
            else
            {
                currentChunk += new Span(child.Span.Start, child.Span.End);
            }
        }
        chunks.Add(currentChunk);
        return chunks;
    }

    private int NonWhitespaceLen(string s)
    {
        return Regex.Replace(s, @"\s", "").Length;
    }

    private List<Span> CoalesceChunks(
        List<Span> chunks, 
        string sourceCode, 
        int coalesce = 50)
    {
        var newChunks = new List<Span>();
        var currentChunk = new Span(0, 0);
        
        foreach (var chunk in chunks)
        {
            currentChunk += chunk;
            if (currentChunk.Length() > coalesce && currentChunk.Extract(sourceCode).Contains("\n"))
            {
                newChunks.Add(currentChunk);
                currentChunk = new Span(chunk.End, chunk.End);
            }
        }
        
        if (currentChunk.Length() > 0)
        {
            newChunks.Add(currentChunk);
        }
        
        return newChunks;
    }

    private int GetLineNumber(int index, string sourceCode)
    {
        int totalChars = 0;
        string[] lines = sourceCode.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        
        for (int lineNumber = 1; lineNumber <= lines.Length; lineNumber++)
        {
            totalChars += lines[lineNumber - 1].Length + 1; // +1 for the newline character
            if (totalChars > index)
            {
                return lineNumber - 1;
            }
        }
        
        return lines.Length;
    }

    public List<Span> Chunker(
        SyntaxNode rootNode,
        string sourceCode,
        int maxChars = 512 * 3,
        int coalesce = 50) // Any chunk less than 50 characters long gets coalesced with the next chunk
    {
        // 1. Recursively form chunks based on the last post
        var chunks = ChunkNode(rootNode, maxChars);

        // 2. Filling in the gaps
        for (int i = 0; i < chunks.Count - 1; i++)
        {
            chunks[i].End = chunks[i + 1].Start;
        }
        if (chunks.Count > 0)
        {
            var lastChunk = chunks[chunks.Count - 1];
            lastChunk.End = rootNode.Span.End;
        }

        // 3. Combining small chunks with bigger ones
        var newChunks = new List<Span>();
        var currentChunk = new Span(0, 0);
        foreach (var chunk in chunks)
        {
            currentChunk += chunk;
            if (NonWhitespaceLen(currentChunk.Extract(sourceCode)) > coalesce && 
                currentChunk.Extract(sourceCode).Contains("\n"))
            {
                newChunks.Add(currentChunk);
                currentChunk = new Span(chunk.End, chunk.End);
            }
        }
        if (currentChunk.Length() > 0)
        {
            newChunks.Add(currentChunk);
        }

        // 4. Changing line numbers
        var lineChunks = newChunks
            .Select(chunk => new Span(
                GetLineNumber(chunk.Start, sourceCode),
                GetLineNumber(chunk.End, sourceCode)))
            .ToList();

        // 5. Eliminating empty chunks
        lineChunks = lineChunks
            .Where(chunk => chunk.Length() > 0)
            .ToList();

        return lineChunks;
    }
}

public class ChunkWithLineNumber
{   
    public string chunk;
    public int startLine;
    public int endLine;

    public ChunkWithLineNumber(string chunk, int startLine, int endLine)
    {
        this.chunk = chunk;
        this.startLine = startLine;
        this.endLine = endLine;
    }
}






/*using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System;
using System.IO;
using UnityEngine;

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
        // Match Python's splitlines() behavior which splits on \n, \r\n, etc.
        var lines = s.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        return string.Join("\n", lines.Skip(Start).Take(End - Start));
    }

    public static Span operator +(Span a, Span b)
    {
        // Ensure exact same behavior as Python's __add__
        if (a.Length() == 0) return b;
        if (b.Length() == 0) return a;
        return new Span(a.Start, b.End);
    }

    public int Length()
    {
        // i.e. Span(a, b) = b - a
        return End - Start;
    }
}

public class CodeChunker
{
    private string PrettyNode(SyntaxNode node)
    {
        return $"{node.Kind()}:{node.Span.Start}-{node.Span.End}";
    }

    private void PrintTree(SyntaxNode node, string indent = "")
    {
        // Using Regex to remove whitespace, similar to Python's re.sub
        var nodeText = Regex.Replace(node.ToString(), @"\s", "");
        if (nodeText.Length < 100)
            return;
            
        Console.WriteLine(indent + PrettyNode(node));
        foreach (var child in node.ChildNodes())
        {
            PrintTree(child, indent + "  ");
        }
    }

    private List<Span> ChunkNode(
        SyntaxNode node,
        int maxChars)
    {
        var chunks = new List<Span>();
        var currentChunk = new Span(node.Span.Start, node.Span.Start);
        var nodeChildren = node.ChildNodes();
        
        foreach (var child in nodeChildren)
        {
            if (child.Span.End - child.Span.Start > maxChars)
            {
                chunks.Add(currentChunk);
                currentChunk = new Span(child.Span.End, child.Span.End);
                chunks.AddRange(ChunkNode(child, maxChars));
            }
            else if (child.Span.End - child.Span.Start + currentChunk.Length() > maxChars)
            {
                chunks.Add(currentChunk);
                currentChunk = new Span(child.Span.Start, child.Span.End);
            }
            else
            {
                currentChunk += new Span(child.Span.Start, child.Span.End);
            }
        }
        chunks.Add(currentChunk);
        return chunks;
    }

    private int NonWhitespaceLen(string s)
    {
        return Regex.Replace(s, @"\s", "").Length;
    }

    public static int GetLineNumber(int index, string sourceCode)
    {
        int totalChars = 0;
        int lineNumber = 1;
        
        foreach (var line in sourceCode.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
        {
            if (totalChars + line.Length >= index)
            {
                return lineNumber - 1;
            }
            
            if (totalChars + line.Length + 1 < sourceCode.Length)
            {
                if (sourceCode[totalChars + line.Length] == '\r' && 
                    totalChars + line.Length + 1 < sourceCode.Length && 
                    sourceCode[totalChars + line.Length + 1] == '\n')
                {
                    totalChars += line.Length + 2;
                }
                else
                {
                    totalChars += line.Length + 1;
                }
            }
            else
            {
                totalChars += line.Length;
            }
            
            lineNumber++;
        }
        return lineNumber - 1;
    }


    public List<Span> Chunker(
        SyntaxNode rootNode,
        string sourceCode,
        int maxChars = 512 * 3,
        int coalesce = 50)
    {
        var chunks = ChunkNode(rootNode, maxChars);

        // 2. Filling in the gaps
        for (int i = 0; i < chunks.Count - 1; i++)
        {
            chunks[i].End = chunks[i + 1].Start;
        }
        if (chunks.Count > 0)
        {
            // Match Python behavior: set the last chunk's start to root node end
            var lastChunk = chunks[chunks.Count - 1];
            lastChunk.Start = rootNode.Span.End;
        }

        // 3. Combining small chunks with bigger ones
        var newChunks = new List<Span>();
        var currentChunk = new Span(0, 0);
        foreach (var chunk in chunks)
        {
            currentChunk += chunk;
            if (NonWhitespaceLen(currentChunk.Extract(sourceCode)) > coalesce && 
                currentChunk.Extract(sourceCode).Contains("\n"))
            {
                newChunks.Add(currentChunk);
                currentChunk = new Span(chunk.End, chunk.End);
            }
        }
        if (currentChunk.Length() > 0)
        {
            newChunks.Add(currentChunk);
        }

        // 4. Changing line numbers
        var lineChunks = newChunks
            .Select(chunk => new Span(
                GetLineNumber(chunk.Start, sourceCode),
                GetLineNumber(chunk.End, sourceCode)))
            .ToList();

        // 5. Eliminating empty chunks
        lineChunks = lineChunks
            .Where(chunk => chunk.Length() > 0)
            .ToList();

        return lineChunks;
    }
}

public class ChunkWithLineNumber
{   
    public string chunk;
    public int startLine;
    public int endLine;

    public ChunkWithLineNumber(string chunk, int startLine, int endLine)
    {
        this.chunk = chunk;
        this.startLine = startLine;
        this.endLine = endLine;
    }
}

*/