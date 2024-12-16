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
        private readonly string keywordColour = "<color=#569CD6>";
        private readonly string identifierColour = "<color=#4EC9B0>";
        private readonly string methodColour = "<color=#DCDCAA>";
        private readonly string stringColour = "<color=#CE9178>";
        private readonly string commentColour = "<color=#6A9955>";
        private readonly string numberColour = "<color=#9CDCFE>";
        private readonly string defaultColour = "<color=#9CDCFE>";


        public string HighlightLine(string lineText)
        {
            try
            {
                var tree = CSharpSyntaxTree.ParseText(lineText);
                var root = tree.GetRoot();
                var formattedLine = new StringBuilder();

                foreach (var token in root.DescendantTokens())
                {
                    foreach (var trivia in token.LeadingTrivia)
                    {
                        formattedLine.Append(ProcessTrivia(trivia));
                    }

                    formattedLine.Append(ProcessToken(token));


                    foreach (var trivia in token.TrailingTrivia)
                    {
                        formattedLine.Append(ProcessTrivia(trivia));
                    }
                }

                return formattedLine.ToString();
            }
            catch
            {
                return lineText;
            }
        }

        private string ProcessToken(SyntaxToken token)
        {
            var tokenText = token.Text;
            var kind = token.Kind();

            if (IsTypeIdentifier(token) || IsClassDeclaration(token) || isBaseType(token) || isAttribute(token))
                return WrapInColor(tokenText, identifierColour);

            if (isMethodDeclaration(token) || isMethodUsage(token) || isInvocation(token))
            {
                return WrapInColor(tokenText, methodColour);
            }

            if (kind == SyntaxKind.NumericLiteralToken)
                return WrapInColor(tokenText, numberColour);

            if (SyntaxFacts.IsKeywordKind(kind))
                return WrapInColor(tokenText, keywordColour);

            if (kind is SyntaxKind.StringLiteralToken or SyntaxKind.InterpolatedStringTextToken or SyntaxKind.InterpolatedStringEndToken or SyntaxKind.InterpolatedStringStartToken)
                return WrapInColor(tokenText, stringColour);

            return tokenText;
        }

        private string ProcessTrivia(SyntaxTrivia trivia)
        {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                return WrapInColor(trivia.ToFullString(), commentColour);
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
                   memberAccess.Name == identifier &&
                   memberAccess.Parent is InvocationExpressionSyntax;
        }
    }
}