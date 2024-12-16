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
        private readonly string keywordColour = "<color=#C678DD>";
        private readonly string identifierColour = "<color=#D19A66>";
        private readonly string methodColour = "<color=#61AFEF>";
        private readonly string stringColour = "<color=#98C379>";
        private readonly string commentColour = "<color=#7F848E>";
        private readonly string numberColour = "<color=#D19A66>";


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

            if (kind == SyntaxKind.AsteriskToken ||
    kind == SyntaxKind.PlusToken ||
    kind == SyntaxKind.MinusToken ||
    kind == SyntaxKind.SlashToken ||
    kind == SyntaxKind.EqualsToken ||
    kind == SyntaxKind.LessThanToken ||
    kind == SyntaxKind.GreaterThanToken)
            {
                return WrapInColor(tokenText, methodColour);
            }

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
                   (identifierName.Parent is VariableDeclarationSyntax || identifierName.Parent is ObjectCreationExpressionSyntax);

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