using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IndieBuff.Editor
{
    public class IndieBuff_SyntaxHighlighter
    {
        private readonly Dictionary<string, string> colorMap;

        public IndieBuff_SyntaxHighlighter()
        {
            colorMap = new Dictionary<string, string>
            {
                ["keyword"] = "<color=#569CD6>",
                ["string"] = "<color=#CE9178>",
                ["number"] = "<color=#B5CEA8>",
                ["comment"] = "<color=#6A9955>",
                ["operator"] = "<color=#D4D4D4>",
                ["type"] = "<color=#4EC9B0>",
                ["default"] = "<color=#9CDCFE>"
            };
        }

        public string HighlightLine(string line)
        {
            StringBuilder highlightedLine = new StringBuilder();
            int index = 0;

            while (index < line.Length)
            {
                if (char.IsWhiteSpace(line[index]))
                {
                    highlightedLine.Append(line[index]);
                    index++;
                }
                else if (char.IsLetter(line[index]) || line[index] == '_')
                {
                    string token = ExtractIdentifier(line, ref index);
                    highlightedLine.Append(ColorToken(token));
                }
                else if (char.IsDigit(line[index]))
                {
                    string token = ExtractNumber(line, ref index);
                    highlightedLine.Append(ColorToken(token));
                }
                else if (line[index] == '"' || line[index] == '\'')
                {
                    string token = ExtractString(line, ref index);
                    highlightedLine.Append(ColorToken(token));
                }
                else if (line.Substring(index).StartsWith("//"))
                {
                    string token = line.Substring(index);
                    highlightedLine.Append(ColorToken(token));
                    break;
                }
                else
                {
                    string token = ExtractOperator(line, ref index);
                    highlightedLine.Append(ColorToken(token));
                }
            }

            return highlightedLine.ToString();
        }



        private string ExtractIdentifier(string line, ref int index)
        {
            int start = index;
            while (index < line.Length && (char.IsLetterOrDigit(line[index]) || line[index] == '_'))
            {
                index++;
            }
            return line.Substring(start, index - start);
        }

        private string ExtractNumber(string line, ref int index)
        {
            int start = index;
            bool hasDecimal = false;
            while (index < line.Length && (char.IsDigit(line[index]) || line[index] == '.'))
            {
                if (line[index] == '.')
                {
                    if (hasDecimal) break;
                    hasDecimal = true;
                }
                index++;
            }
            return line.Substring(start, index - start);
        }

        private string ExtractString(string line, ref int index)
        {
            char quote = line[index];
            int start = index;
            index++;
            while (index < line.Length && line[index] != quote)
            {
                if (line[index] == '\\' && index + 1 < line.Length)
                {
                    index += 2;
                }
                else
                {
                    index++;
                }
            }
            if (index < line.Length) index++;
            return line.Substring(start, index - start);
        }

        private string ExtractOperator(string line, ref int index)
        {
            string[] operators = { "==", "!=", "<=", ">=", "&&", "||", "++", "--", "+=", "-=", "*=", "/=" };
            foreach (string op in operators)
            {
                if (line.Substring(index).StartsWith(op))
                {
                    index += op.Length;
                    return op;
                }
            }
            return line[index++].ToString();
        }

        private string ColorToken(string token)
        {
            string[] keywords = { "public", "private", "void", "string", "int", "bool", "class", "static", "if", "else", "for", "foreach", "while", "return", "var", "new" };
            string[] types = { "string", "int", "bool", "float", "double", "char", "object", "List", "Dictionary" };

            if (keywords.Contains(token))
            {
                return $"{colorMap["keyword"]}{token}</color>";
            }
            else if (types.Contains(token))
            {
                return $"{colorMap["type"]}{token}</color>";
            }
            else if (token.StartsWith("\"") || token.StartsWith("'"))
            {
                return $"{colorMap["string"]}{token}</color>";
            }
            else if (double.TryParse(token, out _))
            {
                return $"{colorMap["number"]}{token}</color>";
            }
            else if (token.StartsWith("//"))
            {
                return $"{colorMap["comment"]}{token}</color>";
            }
            else if (IsOperator(token))
            {
                return $"{colorMap["operator"]}{token}</color>";
            }
            else
            {
                return $"{colorMap["default"]}{token}</color>";
            }
        }

        private bool IsOperator(string token)
        {
            string[] operators = { "+", "-", "*", "/", "%", "=", "==", "!=", "<", ">", "<=", ">=", "&&", "||", "!", "&", "|", "^", "~", "++", "--", "+=", "-=", "*=", "/=" };
            return operators.Contains(token);
        }
    }
}