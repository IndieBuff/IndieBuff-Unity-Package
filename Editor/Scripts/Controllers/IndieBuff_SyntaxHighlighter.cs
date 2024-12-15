using System;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEngine;

namespace IndieBuff.Editor
{
    public class IndieBuff_SyntaxHighlighter
    {
        // Color configuration
        private readonly string k_ColorKeyword = "<color=#569CD6>"; // Azure-like for keywords
        private readonly string k_ColorIdentifier = "<color=#4EC9B0>";
        private readonly string k_ColorMethod = "<color=#DCDCAA>"; // Turquoise-like for methods
        private readonly string k_ColorString = "<color=#CE9178>"; // Sand-like for strings
        private readonly string k_ColorComment = "<color=#6A9955>"; // Lime-like for comments
        private readonly string k_ColorDefault = "<color=#9CDCFE>"; // Default text color

        public string HighlightLine(string lineText)
        {
            try
            {
                var tree = CSharpSyntaxTree.ParseText(lineText);
                var root = tree.GetRoot();
                var formattedLine = new StringBuilder();

                foreach (var token in root.DescendantTokens())
                {

                    // Process leading trivia (whitespace, comments)
                    foreach (var trivia in token.LeadingTrivia)
                    {
                        formattedLine.Append(ProcessTrivia(trivia));
                    }

                    // Process the token itself
                    formattedLine.Append(ProcessToken(token));

                    // Process trailing trivia
                    foreach (var trivia in token.TrailingTrivia)
                    {
                        formattedLine.Append(ProcessTrivia(trivia));
                    }
                }

                return formattedLine.ToString();
            }
            catch
            {
                // Fallback to unmodified line if parsing fails
                return lineText;
            }
        }

        private string ProcessToken(SyntaxToken token)
        {
            var tokenText = token.Text;
            var kind = token.Kind();
            var parent = token.Parent;//
            //Debug.Log(parent.Parent.Parent.Kind() + " - " + token.Kind() + " - " + tokenText);

            if (IsTypeIdentifier(token) || IsClassDeclaration(token) || isBaseType(token) || isAttribute(token))
                return WrapInColor(tokenText, k_ColorIdentifier);

            if (isMethodDeclaration(token) || isMethodUsage(token) || isInvocation(token))
            {
                return WrapInColor(tokenText, k_ColorMethod);
            }

            if (SyntaxFacts.IsKeywordKind(kind))
                return WrapInColor(tokenText, k_ColorKeyword);

            if (kind is SyntaxKind.StringLiteralToken or SyntaxKind.InterpolatedStringTextToken)
                return WrapInColor(tokenText, k_ColorString);

            return tokenText;
        }

        private string ProcessTrivia(SyntaxTrivia trivia)
        {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                return WrapInColor(trivia.ToFullString(), k_ColorComment);
            }

            return trivia.ToFullString();
        }

        private string WrapInColor(string text, string color)
        {
            return $"{color}{text}</color>";
        }

        private bool IsTypeIdentifier(SyntaxToken token)
        {
            return token.Parent is IdentifierNameSyntax identifierName &&
                   identifierName.Parent is VariableDeclarationSyntax;
        }

        private bool IsClassDeclaration(SyntaxToken token)
        {
            return token.Parent is ClassDeclarationSyntax classNode && token == classNode.Identifier;
        }

        private bool isBaseType(SyntaxToken token)
        {
            return token.Parent.Parent is SimpleBaseTypeSyntax;
        }

        private bool isMethodDeclaration(SyntaxToken token)
        {
            return token.Parent is LocalFunctionStatementSyntax methodDeclaration &&
                   methodDeclaration.Identifier == token;
        }

        private bool isMethodUsage(SyntaxToken token)
        {
            return token.Parent is IdentifierNameSyntax identifierName &&
                   identifierName.Identifier == token &&
                   identifierName.Parent is InvocationExpressionSyntax;
        }

        private bool isAttribute(SyntaxToken token)
        {
            return token.Parent.Parent is AttributeSyntax;
        }

        private bool isInvocation(SyntaxToken token)
        {
            return token.Parent is IdentifierNameSyntax identifier &&
                   identifier.Parent is MemberAccessExpressionSyntax memberAccess &&
                   memberAccess.Name == identifier && // Ensure the token is the method name
                   memberAccess.Parent is InvocationExpressionSyntax; // Ensure it's a method call
        }
    }
}


/*
using UnityEngine;

public class ExampleScript : MonoBehaviour
{
    [SerializeField]
    private int health = 100;

    // test comment
    private Vector3 startPosition;

    private void Start()
    {
        InitializePlayer();
    }

    private void InitializePlayer()
    {
        Debug.Log($"{playerName} initialized with {health} health.");
    }
}
*/