using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System;
using System.IO;
using UnityEngine;
using System.Text;

public class IndieBuff_CodeProcessor
{
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

    public static List<IndieBuff_CodeChunk> ProcessScript()
    {

        var projectPath = Application.dataPath;
        Debug.Log($"Searching for .cs files in: {projectPath}");

        // Get all .cs files
        var files = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories);
        Debug.Log($"Found {files.Length} total .cs files before filtering");

        // Filter out Packages and other directories if needed
        var excludePatterns = new[] { 
            "/Packages/",
            "/Plugins/",
            "/ThirdParty/"
        };

        var filteredFiles = files.Where(file => 
            !excludePatterns.Any(pattern => file.Contains(pattern))
        ).ToList();

        Debug.Log($"After filtering, processing {filteredFiles.Count} files");

        List<IndieBuff_CodeChunk> allChunks = new List<IndieBuff_CodeChunk>();

        foreach (var file in filteredFiles)
        {
            var code = File.ReadAllText(file, Encoding.UTF8);
            var chunker = new IndieBuff_CodeProcessor();
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();

            // get relative path
            var relativePath = file.Replace(projectPath, "").Replace("\\", "/");

            var chunks = chunker.Chunker(root, code);
            
            List<string> chunkStrings = chunks.Select(chunk => chunk.ExtractLines(code)).ToList();
            
            // Add file path information to help identify source
            var fileChunks = new List<IndieBuff_CodeChunk>();
            for (int i = 0; i < chunks.Count; i++)
            {
                fileChunks.Add(new IndieBuff_CodeChunk(
                    chunkStrings[i], 
                    chunks[i].Start, 
                    chunks[i].End,
                    "Assets" + relativePath
                ));
            }

            allChunks.AddRange(fileChunks);
            Debug.Log($"Processed {file} - Found {chunks.Count} chunks");
        }

        return allChunks;
    }
}