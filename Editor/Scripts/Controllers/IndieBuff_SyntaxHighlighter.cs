using System;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace IndieBuff.Editor
{
    public class IndieBuff_SyntaxHighlighter
    {
        private readonly string k_ColorKeyword = "<color=#569CD6>";
        private readonly string k_ColorType = "<color=#4EC9B0>";
        private readonly string k_ColorMethod = "<color=#DCDCAA>";
        private readonly string k_ColorString = "<color=#CE9178>";
        private readonly string k_ColorComment = "<color=#6A9955>";

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

            if (SyntaxFacts.IsKeywordKind(kind))
                return WrapInColor(tokenText, k_ColorKeyword);

            if (IsTypeIdentifier(token) || IsMethodReturnType(token) || IsClassDeclaration(token))
                return WrapInColor(tokenText, k_ColorType);

            if (IsMethodDeclaration(token))
                return WrapInColor(tokenText, k_ColorMethod);

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

        private bool IsClassDeclaration(SyntaxToken token)
        {
            return token.Parent is ClassDeclarationSyntax classNode &&
                   token == classNode.Identifier;
        }

        private bool IsMethodDeclaration(SyntaxToken token)
        {
            return token.Parent is MethodDeclarationSyntax methodNode &&
                   token == methodNode.Identifier;
        }

        private bool IsTypeIdentifier(SyntaxToken token)
        {
            return token.Parent is IdentifierNameSyntax identifierName &&
                   identifierName.Parent is VariableDeclarationSyntax;
        }

        private bool IsMethodReturnType(SyntaxToken token) =>
            token.Parent is PredefinedTypeSyntax or IdentifierNameSyntax
            && token.Parent.Parent is MethodDeclarationSyntax methodDeclaration
            && methodDeclaration.ReturnType == token.Parent;
    }
}