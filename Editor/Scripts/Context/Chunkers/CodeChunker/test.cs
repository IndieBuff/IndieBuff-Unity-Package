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

    public static int GetLineNumber(int index, string sourceCode)
    {
        int totalChars = 0;
        int lineNumber = 0;
        
        using (var reader = new StringReader(sourceCode))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                totalChars += line.Length + Environment.NewLine.Length;
                if (totalChars > index)
                {
                    return lineNumber - 1;
                }
            }
        }
        return lineNumber;
    }

    private bool ShouldMergeWithNext(SyntaxNode rootNode, Span currentChunk, Span nextChunk, string sourceCode)
    {
        // Find nodes that intersect with the boundary between chunks
        var boundaryPosition = currentChunk.End;
        var nodesAtBoundary = rootNode.DescendantNodes()
            .Where(node => node.Span.Start < boundaryPosition && node.Span.End > boundaryPosition)
            .ToList();

        // Check if any of these nodes are class or method declarations
        foreach (var node in nodesAtBoundary)
        {
            if (node.IsKind(SyntaxKind.ClassDeclaration) ||
                node.IsKind(SyntaxKind.MethodDeclaration) ||
                node.IsKind(SyntaxKind.ConstructorDeclaration))
            {
                return true;
            }
        }
        return false;
    }

    public List<Span> Chunker(SyntaxNode rootNode, string sourceCode, int maxChars = 512 * 3, int coalesce = 50)
    {
        List<Span> ChunkNode(SyntaxNode node, int maxChars)
        {
            var chunks = new List<Span>();
            var currentChunk = new Span(node.FullSpan.Start, node.FullSpan.Start);  // Use FullSpan instead of Span
            var nodeChildren = node.ChildNodes().ToList();
            
            foreach (var child in nodeChildren)
            {
                // Include leading and trailing trivia in the span calculation
                var childStart = child.FullSpan.Start;
                var childEnd = child.FullSpan.End;
                
                if (childEnd - childStart > maxChars)
                {
                    chunks.Add(currentChunk);
                    currentChunk = new Span(childEnd, childEnd);
                    chunks.AddRange(ChunkNode(child, maxChars));
                }
                else if (childEnd - childStart + currentChunk.Length() > maxChars)
                {
                    chunks.Add(currentChunk);
                    currentChunk = new Span(childStart, childEnd);
                }
                else
                {
                    currentChunk += new Span(childStart, childEnd);
                }
            }
            chunks.Add(currentChunk);
            return chunks;
        }

        void MergeChunksRecursively(List<Span> chunks, List<Span> lineChunks, int index)
        {
            if (index < 0 || index >= chunks.Count) return;

            string currentText = chunks[index].Extract(sourceCode);
            
            // If current chunk is below max chars, try to merge it
            if (currentText.Length < maxChars)
            {
                // Try to merge with previous chunk if possible
                if (index > 0)
                {
                    string combinedWithPrev = chunks[index - 1].Extract(sourceCode) + currentText;
                    if (combinedWithPrev.Length < maxChars)
                    {
                        // Merge byte spans
                        chunks[index - 1].End = chunks[index].End;
                        chunks.RemoveAt(index);
                        
                        // Update line numbers based on the actual byte positions
                        lineChunks[index - 1].Start = GetLineNumber(chunks[index - 1].Start, sourceCode);
                        lineChunks[index - 1].End = GetLineNumber(chunks[index - 1].End, sourceCode);
                        lineChunks.RemoveAt(index);
                        
                        // Recursively check previous chunk again
                        MergeChunksRecursively(chunks, lineChunks, index - 1);
                        return;
                    }
                }
                
                // If we couldn't merge with previous, try to merge with next chunk
                if (index < chunks.Count - 1)
                {
                    string combinedWithNext = currentText + chunks[index + 1].Extract(sourceCode);
                    bool shouldMerge = ShouldMergeWithNext(rootNode, chunks[index], chunks[index + 1], sourceCode);
                    
                    if (combinedWithNext.Length < maxChars || shouldMerge)
                    {
                        // Merge byte spans
                        chunks[index].End = chunks[index + 1].End;
                        chunks.RemoveAt(index + 1);
                        
                        // Update line numbers based on the actual byte positions
                        lineChunks[index].Start = GetLineNumber(chunks[index].Start, sourceCode);
                        lineChunks[index].End = GetLineNumber(chunks[index].End, sourceCode);
                        lineChunks.RemoveAt(index + 1);
                        
                        // Recursively check current chunk again
                        MergeChunksRecursively(chunks, lineChunks, index);
                    }
                }
            }
        }

        var initialChunks = ChunkNode(rootNode, maxChars);
        
        // First pass: Fill gaps
        for (int i = 0; i < initialChunks.Count - 1; i++)
        {
            initialChunks[i].End = initialChunks[i + 1].Start;
        }
        if (initialChunks.Count > 0)
        {
            initialChunks[initialChunks.Count - 1].End = rootNode.FullSpan.End;
        }

        // Convert to line numbers before merging
        var lineChunks = initialChunks
            .Select(chunk => new Span(
                GetLineNumber(chunk.Start, sourceCode),
                GetLineNumber(chunk.End, sourceCode)))
            .ToList();

        // Second pass: Recursively merge chunks
        var processedChunks = new List<Span>(initialChunks);
        for (int i = 0; i < processedChunks.Count; i++)
        {
            MergeChunksRecursively(processedChunks, lineChunks, i);
        }

        return lineChunks.Where(chunk => chunk.Length() > 0).ToList();
    }

    public static void DebugPrintNodeStructure(SyntaxNode node, string indent = "")
    {
        Debug.Log($"{indent}Node: {node.Kind()} ({node.Span.Start}-{node.Span.End})");
        Debug.Log($"{indent}Text: '{node.ToString()}'");
        
        foreach (var child in node.ChildNodes())
        {
            DebugPrintNodeStructure(child, indent + "  ");
        }
    }
}

public class ChunkWithLineNumber
{   
    public string chunk;
    public int startLine;
    public int endLine;
    public string filePath;

    public ChunkWithLineNumber(string chunk, int startLine, int endLine, string filePath)
    {
        this.chunk = chunk;
        this.startLine = startLine;
        this.endLine = endLine;
        this.filePath = filePath;
    }
}

public class LineNumber
{   
    public int startLine;
    public int endLine;

    public LineNumber(int startLine, int endLine)
    {
        this.startLine = startLine;
        this.endLine = endLine;
    }
}