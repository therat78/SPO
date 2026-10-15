using System;
using System.Collections.Generic;

namespace first
{
    public sealed class AnalysisResult
    {
        public int IfCount { get; internal set; }
        public int LoopCount { get; internal set; }
        public int SwitchCount { get; internal set; }
        public int SwitchCaseCount { get; internal set; }
        public int TernaryCount { get; internal set; }
        public int TotalStatements { get; internal set; }
        public int AbsoluteComplexity { get; internal set; }
        public int MaxNesting { get; internal set; }
        public IList<string> Warnings { get; internal set; }

        public double RelativeComplexity
        {
            get
            {
                return TotalStatements == 0 ? 0.0 :
                    (double)AbsoluteComplexity / TotalStatements;
            }
        }

        internal AnalysisResult()
        {
            Warnings = new List<string>();
        }
    }

    public static class FSharpAnalyzer
    {
        public static AnalysisResult Analyze(string source)
        {
            AnalysisResult result = new AnalysisResult();
            FSharpLexer lexer = new FSharpLexer(source ?? String.Empty);
            List<FSharpToken> tokens = lexer.Tokenize();
            result.Warnings = lexer.Warnings;

            FSharpParser parser = new FSharpParser(tokens, result);
            parser.Parse();

            // Формула исходного анализатора не изменена.
            result.AbsoluteComplexity = result.IfCount + result.LoopCount +
                result.SwitchCaseCount + result.TernaryCount;
            return result;
        }
    }

    internal enum FSharpTokenKind
    {
        Identifier, Number, String, Symbol, NewLine
    }

    internal sealed class FSharpToken
    {
        public string Text { get; private set; }
        public FSharpTokenKind Kind { get; private set; }
        public int Line { get; private set; }
        public int Column { get; private set; }
        public bool IsQuotedIdentifier { get; private set; }

        public FSharpToken(string text, FSharpTokenKind kind, int line, int column,
            bool isQuotedIdentifier = false)
        {
            Text = text;
            Kind = kind;
            Line = line;
            Column = column;
            IsQuotedIdentifier = isQuotedIdentifier;
        }
    }

    internal sealed class FSharpLexer
    {
        private readonly string _source;
        private readonly List<FSharpToken> _tokens = new List<FSharpToken>();
        private readonly List<string> _warnings = new List<string>();
        private int _index;
        private int _line = 1;
        private int _column = 1;

        public List<string> Warnings { get { return _warnings; } }

        public FSharpLexer(string source) { _source = source; }

        public List<FSharpToken> Tokenize()
        {
            while (_index < _source.Length)
            {
                char c = Current;
                if (c == ' ' || c == '\f' || c == '\uFEFF')
                {
                    Advance();
                    continue;
                }
                if (c == '\t')
                {
                    Warn("Табуляция в отступе: используйте пробелы", _line, _column);
                    Advance();
                    continue;
                }
                if (IsNewLine(c))
                {
                    EmitNewLine();
                    continue;
                }
                if (c == '/' && Peek(1) == '/')
                {
                    while (_index < _source.Length && !IsNewLine(Current)) Advance();
                    continue;
                }
                if (c == '(' && Peek(1) == '*')
                {
                    ReadComment();
                    continue;
                }
                if (c == '`' && Peek(1) == '`')
                {
                    ReadQuotedIdentifier();
                    continue;
                }
                if (IsStringStart())
                {
                    ReadString();
                    continue;
                }
                if (c == '\'' && IsCharacterLiteral())
                {
                    ReadCharacter();
                    continue;
                }
                if (Char.IsLetter(c) || c == '_' || c == '\'')
                {
                    ReadIdentifier();
                    continue;
                }
                if (Char.IsDigit(c))
                {
                    ReadNumber();
                    continue;
                }
                ReadSymbol();
            }
            return _tokens;
        }

        private char Current { get { return _source[_index]; } }
        private char Peek(int offset)
        {
            int at = _index + offset;
            return at >= 0 && at < _source.Length ? _source[at] : '\0';
        }
        private static bool IsNewLine(char c) { return c == '\r' || c == '\n'; }

        private void Advance()
        {
            if (_index >= _source.Length) return;
            char c = _source[_index++];
            if (c == '\r')
            {
                if (_index < _source.Length && _source[_index] == '\n') _index++;
                _line++;
                _column = 1;
            }
            else if (c == '\n')
            {
                _line++;
                _column = 1;
            }
            else if (c == '\t') _column += 4;
            else _column++;
        }

        private void EmitNewLine()
        {
            _tokens.Add(new FSharpToken("\n", FSharpTokenKind.NewLine, _line, _column));
            Advance();
        }

        private void Warn(string text, int line, int column)
        {
            _warnings.Add(text + " (строка " + line + ", столбец " + column + ").");
        }

        private void ReadComment()
        {
            int line = _line, column = _column, depth = 1;
            Advance(); Advance();
            while (_index < _source.Length && depth > 0)
            {
                if (Current == '(' && Peek(1) == '*')
                {
                    depth++;
                    Advance(); Advance();
                }
                else if (Current == '*' && Peek(1) == ')')
                {
                    depth--;
                    Advance(); Advance();
                }
                else if (IsNewLine(Current)) EmitNewLine();
                else Advance();
            }
            if (depth != 0) Warn("Незакрытый комментарий (* *)", line, column);
        }

        private void ReadQuotedIdentifier()
        {
            int line = _line, column = _column, start = _index;
            Advance(); Advance();
            bool closed = false;
            while (_index < _source.Length && !IsNewLine(Current))
            {
                if (Current == '`' && Peek(1) == '`')
                {
                    Advance(); Advance();
                    closed = true;
                    break;
                }
                Advance();
            }
            if (!closed) Warn("Незакрытый идентификатор ``...``", line, column);
            _tokens.Add(new FSharpToken(_source.Substring(start, _index - start),
                FSharpTokenKind.Identifier, line, column, true));
        }

        private bool IsStringStart()
        {
            if (Current == '"') return true;
            int p = _index;
            if (_source[p] == '@')
            {
                p++;
                if (p < _source.Length && _source[p] == '$') p++;
            }
            else if (_source[p] == '$')
            {
                while (p < _source.Length && _source[p] == '$') p++;
                if (p < _source.Length && _source[p] == '@') p++;
            }
            else return false;
            return p < _source.Length && _source[p] == '"';
        }

        private void ReadString(bool addToken = true)
        {
            int line = _line, column = _column;
            bool verbatim = false, interpolated = false;
            while (_index < _source.Length && (Current == '$' || Current == '@'))
            {
                if (Current == '@') verbatim = true;
                if (Current == '$') interpolated = true;
                Advance();
            }
            bool triple = Peek(1) == '"' && Peek(2) == '"';
            Advance();
            if (triple) { Advance(); Advance(); }
            bool closed = false;
            while (_index < _source.Length)
            {
                if (triple && Current == '"' && Peek(1) == '"' && Peek(2) == '"')
                {
                    Advance(); Advance(); Advance();
                    closed = true;
                    break;
                }
                if (!triple && Current == '"')
                {
                    if (verbatim && Peek(1) == '"')
                    {
                        Advance(); Advance();
                        continue;
                    }
                    Advance();
                    closed = true;
                    break;
                }
                if (!triple && interpolated && Current == '{')
                {
                    if (Peek(1) == '{') { Advance(); Advance(); }
                    else SkipInterpolation();
                    continue;
                }
                if (!triple && !verbatim && Current == '\\')
                {
                    Advance();
                    if (_index < _source.Length) Advance();
                }
                else Advance();
            }
            if (!closed) Warn("Незакрытая строка", line, column);
            if (interpolated && addToken)
                Warn("Интерполируемая строка анализируется как единый литерал; " +
                    "выражения внутри интерполяций не учитываются", line, column);
            if (addToken)
                _tokens.Add(new FSharpToken("<строка>", FSharpTokenKind.String, line, column));
        }

        private void SkipInterpolation()
        {
            int line = _line, column = _column, depth = 1;
            Advance();
            while (_index < _source.Length && depth > 0)
            {
                if (IsStringStart()) { ReadString(false); continue; }
                if (Current == '(' && Peek(1) == '*')
                {
                    int count = _tokens.Count;
                    ReadComment();
                    _tokens.RemoveRange(count, _tokens.Count - count);
                    continue;
                }
                if (Current == '/' && Peek(1) == '/')
                {
                    while (_index < _source.Length && !IsNewLine(Current)) Advance();
                    continue;
                }
                if (Current == '\'' && IsCharacterLiteral())
                {
                    ReadCharacter();
                    _tokens.RemoveAt(_tokens.Count - 1);
                    continue;
                }
                if (Current == '{') depth++;
                else if (Current == '}') depth--;
                Advance();
            }
            if (depth != 0) Warn("Незакрытая интерполяция", line, column);
        }

        private bool IsCharacterLiteral()
        {
            // 'a' — символ; 'T и 'value — параметры типа, а не строки.
            if (Peek(1) == '\0' || IsNewLine(Peek(1))) return false;
            if (Peek(1) != '\\') return Peek(2) == '\'';
            int p = _index + 2;
            if (p >= _source.Length) return false;
            char escape = _source[p++];
            if (escape == 'u') p += 4;
            else if (escape == 'U') p += 8;
            else if (escape == 'x') p += 2;
            else if (Char.IsDigit(escape)) p += 2;
            return p < _source.Length && _source[p] == '\'';
        }

        private void ReadCharacter()
        {
            int line = _line, column = _column;
            Advance();
            if (Current == '\\')
            {
                Advance();
                char escape = Current;
                Advance();
                int extra = escape == 'u' ? 4 : escape == 'U' ? 8 :
                    escape == 'x' || Char.IsDigit(escape) ? 2 : 0;
                while (extra-- > 0 && _index < _source.Length) Advance();
            }
            else Advance();
            if (_index < _source.Length && Current == '\'') Advance();
            _tokens.Add(new FSharpToken("<символ>", FSharpTokenKind.String, line, column));
        }

        private void ReadIdentifier()
        {
            int start = _index, line = _line, column = _column;
            Advance();
            while (_index < _source.Length &&
                (Char.IsLetterOrDigit(Current) || Current == '_' || Current == '\'')) Advance();
            _tokens.Add(new FSharpToken(_source.Substring(start, _index - start),
                FSharpTokenKind.Identifier, line, column));
        }

        private void ReadNumber()
        {
            int start = _index, line = _line, column = _column;
            while (_index < _source.Length)
            {
                char c = Current;
                if (Char.IsLetterOrDigit(c) || c == '_') { Advance(); continue; }
                if (c == '.' && Peek(1) != '.' && Char.IsDigit(Peek(1)))
                {
                    Advance();
                    continue;
                }
                if ((c == '+' || c == '-') && _index > start &&
                    (_source[_index - 1] == 'e' || _source[_index - 1] == 'E'))
                {
                    Advance();
                    continue;
                }
                break;
            }
            _tokens.Add(new FSharpToken(_source.Substring(start, _index - start),
                FSharpTokenKind.Number, line, column));
        }

        private static bool IsOperatorChar(char c)
        {
            return "!%&*+-./<=>?@^|~:$".IndexOf(c) >= 0;
        }

        private void ReadSymbol()
        {
            int start = _index, line = _line, column = _column;
            // Составные скобки F# должны оставаться отдельными токенами.
            string pair = _index + 1 < _source.Length ? _source.Substring(_index, 2) : "";
            if (pair == "[|" || pair == "|]" || pair == "[<" || pair == ">]")
            {
                Advance(); Advance();
            }
            else if (IsOperatorChar(Current))
            {
                Advance();
                while (_index < _source.Length && IsOperatorChar(Current))
                {
                    if (Current == '|' && Peek(1) == ']') break;
                    if (Current == '>' && Peek(1) == ']') break;
                    if (Current == '/' && (Peek(1) == '/' || Peek(1) == '*')) break;
                    Advance();
                }
            }
            else Advance();
            _tokens.Add(new FSharpToken(_source.Substring(start, _index - start),
                FSharpTokenKind.Symbol, line, column));
        }
    }

    // Лёгкий структурный парсер, а не замена FSharp.Compiler.Service.
    // Поддерживает обычный синтаксис F# с существенными отступами.
    internal sealed class FSharpParser
    {
        private readonly List<FSharpToken> _tokens;
        private readonly AnalysisResult _result;
        private readonly Dictionary<int, int> _pairs = new Dictionary<int, int>();
        private int _recursion;
        private bool _reportedDepthLimit;

        private sealed class Arm
        {
            public int PatternStart;
            public int Arrow;
            public int End;
            public bool IsDefault;
            public int Ordinal;
        }

        public FSharpParser(List<FSharpToken> tokens, AnalysisResult result)
        {
            _tokens = tokens;
            _result = result;
        }

        public void Parse()
        {
            CheckDelimiters();
            ParseSequence(0, _tokens.Count, 0);
        }

        private bool Enter()
        {
            if (_recursion >= 256)
            {
                if (!_reportedDepthLimit)
                {
                    _result.Warnings.Add("Превышена допустимая глубина разбора (256); часть кода пропущена.");
                    _reportedDepthLimit = true;
                }
                return false;
            }
            _recursion++;
            return true;
        }

        private void CheckDelimiters()
        {
            Stack<int> stack = new Stack<int>();
            for (int i = 0; i < _tokens.Count; i++)
            {
                string text = Text(i);
                if (IsOpen(text)) stack.Push(i);
                else if (IsClose(text))
                {
                    if (stack.Count == 0 || !Matches(Text(stack.Peek()), text))
                        Warn(i, "Лишняя или несогласованная закрывающая скобка '" + text + "'");
                    else
                    {
                        int open = stack.Pop();
                        _pairs[open] = i;
                    }
                }
            }
            while (stack.Count > 0)
            {
                int i = stack.Pop();
                Warn(i, "Не закрыта скобка '" + Text(i) + "'");
            }
        }

        private void ParseSequence(int start, int end, int depth)
        {
            if (!Enter()) return;
            try
            {
                int p = SkipSeparators(start, end);
                while (p < end)
                {
                    int next = StatementEnd(p, end);
                    if (next <= p) next = p + 1;
                    ParseStatement(p, next, depth);
                    p = SkipSeparators(next, end);
                }
            }
            finally { _recursion--; }
        }

        private int StatementEnd(int start, int end)
        {
            int column = _tokens[start].Column;
            for (int i = start; i < end; i++)
            {
                int close;
                if (TryGroup(i, end, out close)) { i = close; continue; }
                if (At(i, ";") || At(i, ";;")) return i;
                if (!IsNewLine(i)) continue;
                int next = SkipNewLines(i + 1, end);
                if (next >= end) return end;
                int prev = Previous(i - 1, start);
                if (IsContinuation(prev, next)) continue;
                if (_tokens[next].Column <= column) return i;
            }
            return end;
        }

        private bool IsContinuation(int previous, int next)
        {
            string n = Text(next), p = Text(previous);
            if (OneOf(n, "then", "else", "elif", "with", "finally", "|", "in")) return true;
            if (OneOf(n, ".", "|>", "||>", "|||>", "<|", "&&", "||")) return true;
            if (OneOf(p, "=", "->", "<-", "then", "else", "do", "with", "in",
                "try", "finally", ",", ":", "|>", "<|", "+", "-", "*", "/",
                "&&", "||", "yield", "return", "yield!", "return!")) return true;
            return false;
        }

        private void ParseStatement(int start, int end, int depth)
        {
            Trim(ref start, ref end);
            if (start >= end) return;
            if (At(start, "[<"))
            {
                int close;
                if (TryGroup(start, end, out close))
                    ParseStatement(SkipNewLines(close + 1, end), end, depth);
                return;
            }
            string first = Text(start);
            if (OneOf(first, "open", "namespace") || At(start, "#")) return;
            if (OneOf(first, "module", "type", "exception"))
            {
                ParseDeclaration(start, end);
                return;
            }
            if (OneOf(first, "let", "use", "and"))
            {
                ParseBinding(start, end, depth);
                return;
            }
            if (OneOf(first, "member", "override", "abstract", "default", "new", "static") &&
                LooksLikeMember(start, end))
            {
                int eq = FindTop(start + 1, end, "=");
                if (eq >= 0) ParseSequence(eq + 1, end, 0);
                return;
            }
            if (OneOf(first, "if", "elif", "for", "while", "match", "function", "try"))
            {
                ParseExpression(start, end, depth);
                return;
            }
            if (first == "do" && !At(start + 1, "!"))
            {
                ParseSequence(start + 1, end, depth);
                return;
            }
            _result.TotalStatements++;
            ParseExpression(start, end, depth);
        }

        private bool LooksLikeMember(int start, int end)
        {
            return FindTop(start, end, "=") >= 0 || At(start, "abstract");
        }

        private void ParseDeclaration(int start, int end)
        {
            int eq = FindTop(start + 1, end, "=");
            if (eq < 0) return;
            int body = SkipNewLines(eq + 1, end);
            if (At(start, "module"))
            {
                ParseSequence(body, end, 0);
                return;
            }
            // Объявления record/union/enum не являются выполняемыми операторами.
            // У классов анализируем только let/do и тела членов.
            for (int i = body; i < end; i++)
            {
                int close;
                if (TryGroup(i, end, out close)) { i = close; continue; }
                if (!IsLineStart(i)) continue;
                if (OneOf(Text(i), "let", "do", "member", "override", "static", "new"))
                {
                    int memberEnd = StatementEnd(i, end);
                    ParseStatement(i, memberEnd, 0);
                    i = Math.Max(i, memberEnd - 1);
                }
            }
        }

        private void ParseBinding(int start, int end, int depth)
        {
            // let ... in ...: часть после in является отдельным выражением.
            int eq = FindTop(start + 1, end, "=");
            if (eq < 0)
            {
                Warn(start, "В объявлении привязки не найден символ '='");
                return;
            }
            int rhs = SkipNewLines(eq + 1, end);
            int inToken = FindBindingIn(rhs, end);
            int rhsEnd = inToken >= 0 ? inToken : end;
            bool isFunction = IsFunctionBinding(start, eq) || At(rhs, "fun") || At(rhs, "function");
            if (!isFunction) _result.TotalStatements++;

            int bodyDepth = isFunction ? 0 : depth;
            if (isFunction && At(rhs, "fun"))
            {
                int arrow = FindTop(rhs + 1, rhsEnd, "->");
                if (arrow >= 0) ParseSequence(arrow + 1, rhsEnd, bodyDepth);
                else Warn(rhs, "В lambda-выражении не найден символ '->'");
            }
            else if (isFunction) ParseSequence(rhs, rhsEnd, bodyDepth);
            else ParseExpression(rhs, rhsEnd, bodyDepth);

            if (inToken >= 0) ParseSequence(inToken + 1, end, depth);
        }

        private int FindBindingIn(int start, int end)
        {
            int nestedLets = 0;
            for (int i = start; i < end; i++)
            {
                int close;
                if (TryGroup(i, end, out close)) { i = close; continue; }
                if (At(i, "let") || At(i, "use")) nestedLets++;
                if (!At(i, "in")) continue;
                // in в заголовке for — не разделитель локальной привязки.
                bool loopHeader = false;
                for (int j = Previous(i - 1, start); j >= start; j--)
                {
                    if (At(j, "do") || At(j, "->") || At(j, "=")) break;
                    if (At(j, "for")) { loopHeader = true; break; }
                }
                if (loopHeader) continue;
                if (nestedLets == 0) return i;
                nestedLets--;
            }
            return -1;
        }

        private bool IsFunctionBinding(int start, int eq)
        {
            int name = start + 1;
            if (At(name, "!")) return false; // let!/use! — не объявление функции.
            while (name < eq && OneOf(Text(name), "rec", "inline", "mutable", "private",
                "internal", "public")) name++;
            // Активный образец: let (|A|B|) value = ...
            int patternClose;
            if (At(name, "(") && At(name + 1, "|") &&
                TryGroup(name, eq, out patternClose))
                return SkipNewLines(patternClose + 1, eq) < eq;
            // Деструктурирующие привязки не являются объявлениями функций.
            if (name >= eq || _tokens[name].Kind != FSharpTokenKind.Identifier) return false;
            int next = SkipNewLines(name + 1, eq);
            if (next >= eq || At(next, ":")) return false;
            return _tokens[next].Kind == FSharpTokenKind.Identifier ||
                At(next, "(") || At(next, "<");
        }

        private void ParseExpression(int start, int end, int depth)
        {
            Trim(ref start, ref end);
            if (start >= end || !Enter()) return;
            try
            {
                for (int i = start; i < end; i++)
                {
                    int close;
                    if (TryGroup(i, end, out close))
                    {
                        ParseGroup(i, close, depth);
                        i = close;
                        continue;
                    }
                    if (At(i, "if") || At(i, "elif"))
                    {
                        ParseIf(i, end, depth);
                        return;
                    }
                    if (At(i, "for") || At(i, "while"))
                    {
                        ParseLoop(i, end, depth);
                        return;
                    }
                    if (At(i, "match") || At(i, "function"))
                    {
                        ParseMatch(i, end, depth);
                        return;
                    }
                    if (At(i, "try"))
                    {
                        ParseTry(i, end, depth);
                        return;
                    }
                    if (At(i, "fun"))
                    {
                        int arrow = FindTop(i + 1, end, "->");
                        if (arrow < 0) Warn(i, "В lambda-выражении не найден символ '->'");
                        else ParseSequence(arrow + 1, end, depth);
                        return;
                    }
                    if (At(i, "let") || At(i, "use"))
                    {
                        ParseBinding(i, end, depth);
                        return;
                    }
                }
            }
            finally { _recursion--; }
        }

        private void ParseGroup(int open, int close, int depth)
        {
            int start = SkipNewLines(open + 1, close);
            if (start >= close) return;
            // Выражения в аргументах/кортежах разделены запятыми; в массивах
            // и списках — точками с запятой. Сам аргумент не добавляет оператор.
            int part = start;
            bool foundSeparator = false;
            for (int i = start; i < close; i++)
            {
                int nestedClose;
                if (TryGroup(i, close, out nestedClose)) { i = nestedClose; continue; }
                if (!At(i, ",") && !At(i, ";")) continue;
                ParseExpression(part, i, depth);
                part = i + 1;
                foundSeparator = true;
            }
            if (foundSeparator) ParseExpression(part, close, depth);
            else if (At(open, "{") || At(start, "let") || At(start, "use"))
                ParseSequence(start, close, depth);
            else ParseExpression(start, close, depth);
        }

        private void ParseIf(int start, int end, int depth)
        {
            _result.IfCount++;
            _result.TotalStatements++;
            ObserveNesting(depth);
            int then = FindTop(start + 1, end, "then");
            if (then < 0)
            {
                Warn(start, "После if/elif не найдено then");
                ParseExpression(start + 1, end, depth);
                return;
            }
            ParseExpression(start + 1, then, depth);
            int alternative = FindElse(then + 1, end);
            ParseSequence(then + 1, alternative >= 0 ? alternative : end, depth + 1);
            if (alternative >= 0)
            {
                if (At(alternative, "elif")) ParseIf(alternative, end, depth + 1);
                else
                {
                    // else — отдельный оператор в N, но не слагаемое CL.
                    _result.TotalStatements++;
                    ParseSequence(alternative + 1, end, depth + 1);
                }
            }
        }

        private int FindElse(int start, int end)
        {
            Stack<int> nested = new Stack<int>();
            for (int i = start; i < end; i++)
            {
                int close;
                if (TryGroup(i, end, out close)) { i = close; continue; }
                if (At(i, "if")) nested.Push(i);
                if (!At(i, "else") && !At(i, "elif")) continue;
                // Offside закрывает внутренний if без else.
                while (nested.Count > 0 && _tokens[i].Line > _tokens[nested.Peek()].Line &&
                    _tokens[i].Column < _tokens[nested.Peek()].Column)
                    nested.Pop();
                if (nested.Count == 0) return i;
                if (At(i, "else")) nested.Pop();
            }
            return -1;
        }

        private void ParseLoop(int start, int end, int depth)
        {
            _result.LoopCount++;
            _result.TotalStatements++;
            ObserveNesting(depth);
            int body = FindTop(start + 1, end, "do");
            if (body < 0 && At(start, "for")) body = FindTop(start + 1, end, "->");
            if (body < 0)
            {
                Warn(start, "Не найдено тело цикла (do или ->)");
                ParseExpression(start + 1, end, depth);
                return;
            }
            ParseExpression(start + 1, body, depth);
            ParseSequence(body + 1, end, depth + 1);
        }

        private void ParseMatch(int start, int end, int depth)
        {
            _result.SwitchCount++;
            _result.TotalStatements++;
            int labels;
            if (At(start, "function")) labels = start + 1;
            else
            {
                int with = FindTop(start + 1, end, "with");
                if (with < 0)
                {
                    Warn(start, "После match не найдено with");
                    ParseExpression(start + 1, end, depth);
                    return;
                }
                ParseExpression(start + 1, with, depth);
                labels = with + 1;
            }
            List<Arm> arms = ReadArms(labels, end);
            int count = 0;
            for (int i = 0; i < arms.Count; i++)
                if (!arms[i].IsDefault) arms[i].Ordinal = count++;
            _result.SwitchCaseCount += count;
            // В N учитываются все ветки, включая безусловную _.
            // В CL по-прежнему входят только ветки без безусловной _.
            _result.TotalStatements += arms.Count;
            if (count > 0) ObserveNesting(depth + count - 1);
            if (arms.Count == 0) Warn(start, "Не найдены ветки сопоставления с образцом");
            for (int i = 0; i < arms.Count; i++)
            {
                Arm arm = arms[i];
                // when добавляет оператор в N, но не отдельный if в CL.
                int when = FindTop(arm.PatternStart, arm.Arrow, "when");
                if (when >= 0)
                {
                    _result.TotalStatements++;
                    ParseExpression(when + 1, arm.Arrow, depth);
                }
                int bodyDepth = arm.IsDefault ? depth + count : depth + arm.Ordinal + 1;
                ParseSequence(arm.Arrow + 1, arm.End, bodyDepth);
            }
        }

        private List<Arm> ReadArms(int start, int end)
        {
            List<Arm> arms = new List<Arm>();
            start = SkipNewLines(start, end);
            if (start >= end) return arms;
            // | может находиться дальше вправо у первой inline-ветки.
            // Для многострочных веток используем минимальный отступ меток.
            int labelColumn = Int32.MaxValue;
            for (int i = start; i < end; i++)
            {
                int close;
                if (TryGroup(i, end, out close)) { i = close; continue; }
                if (At(i, "|") && IsLineStart(i))
                    labelColumn = Math.Min(labelColumn, _tokens[i].Column);
            }
            int p = start;
            while (p < end)
            {
                p = SkipNewLines(p, end);
                if (At(p, "|")) p++;
                int pattern = SkipNewLines(p, end);
                int arrow = FindTop(pattern, end, "->");
                if (arrow < 0)
                {
                    Warn(pattern < end ? pattern : start, "В ветке не найден символ '->'");
                    break;
                }
                int next = end;
                for (int i = arrow + 1; i < end; i++)
                {
                    int close;
                    if (TryGroup(i, end, out close)) { i = close; continue; }
                    if (!At(i, "|")) continue;
                    bool sameLine = _tokens[i].Line == _tokens[arrow].Line;
                    bool ownIndent = IsLineStart(i) && _tokens[i].Column == labelColumn;
                    if (sameLine || ownIndent) { next = i; break; }
                }
                int patternEnd = Previous(arrow - 1, pattern) + 1;
                bool isDefault = patternEnd == pattern + 1 && At(pattern, "_");
                arms.Add(new Arm
                {
                    PatternStart = pattern,
                    Arrow = arrow,
                    End = next,
                    IsDefault = isDefault,
                    Ordinal = -1
                });
                p = next;
            }
            return arms;
        }

        private void ParseTry(int start, int end, int depth)
        {
            _result.TotalStatements++;
            int handler = FindTryHandler(start + 1, end);
            if (handler < 0)
            {
                Warn(start, "После try не найдено with/finally");
                ParseSequence(start + 1, end, depth);
                return;
            }
            ParseSequence(start + 1, handler, depth);
            if (At(handler, "finally")) ParseSequence(handler + 1, end, depth);
            else
            {
                List<Arm> arms = ReadArms(handler + 1, end);
                for (int i = 0; i < arms.Count; i++)
                {
                    int when = FindTop(arms[i].PatternStart, arms[i].Arrow, "when");
                    if (when >= 0)
                    {
                        _result.TotalStatements++;
                        ParseExpression(when + 1, arms[i].Arrow, depth);
                    }
                    ParseSequence(arms[i].Arrow + 1, arms[i].End, depth);
                }
                if (arms.Count == 0) Warn(handler, "Не найдены обработчики исключений");
            }
        }

        private int FindTryHandler(int start, int end)
        {
            int pendingTries = 0, pendingMatches = 0;
            for (int i = start; i < end; i++)
            {
                int close;
                if (TryGroup(i, end, out close)) { i = close; continue; }
                if (At(i, "try")) pendingTries++;
                else if (At(i, "match")) pendingMatches++;
                else if (At(i, "with") || At(i, "finally"))
                {
                    if (At(i, "with") && pendingMatches > 0) pendingMatches--;
                    else if (pendingTries > 0) pendingTries--;
                    else return i;
                }
            }
            return -1;
        }

        private int FindTop(int start, int end, string text)
        {
            for (int i = start; i < end; i++)
            {
                if (At(i, text)) return i;
                int close;
                if (TryGroup(i, end, out close)) i = close;
            }
            return -1;
        }

        private bool TryGroup(int open, int end, out int close)
        {
            return _pairs.TryGetValue(open, out close) && close < end;
        }

        private void ObserveNesting(int depth)
        {
            if (depth > _result.MaxNesting) _result.MaxNesting = depth;
        }

        private void Warn(int index, string text)
        {
            if (index >= 0 && index < _tokens.Count)
                _result.Warnings.Add(text + " (строка " + _tokens[index].Line +
                    ", столбец " + _tokens[index].Column + ").");
            else _result.Warnings.Add(text + ".");
        }

        private string Text(int i)
        {
            return i >= 0 && i < _tokens.Count && !_tokens[i].IsQuotedIdentifier &&
                _tokens[i].Kind != FSharpTokenKind.String ? _tokens[i].Text : "";
        }
        private bool At(int i, string text) { return Text(i) == text; }
        private bool IsNewLine(int i)
        {
            return i >= 0 && i < _tokens.Count && _tokens[i].Kind == FSharpTokenKind.NewLine;
        }
        private int SkipNewLines(int i, int end)
        {
            while (i < end && IsNewLine(i)) i++;
            return i;
        }
        private int SkipSeparators(int i, int end)
        {
            while (i < end && (IsNewLine(i) || At(i, ";") || At(i, ";;"))) i++;
            return i;
        }
        private int Previous(int i, int start)
        {
            while (i >= start && IsNewLine(i)) i--;
            return i;
        }
        private void Trim(ref int start, ref int end)
        {
            start = SkipSeparators(start, end);
            while (end > start && (IsNewLine(end - 1) || At(end - 1, ";") || At(end - 1, ";;"))) end--;
        }
        private bool IsLineStart(int i)
        {
            return i == 0 || (i > 0 && _tokens[i - 1].Line < _tokens[i].Line);
        }
        private static bool OneOf(string value, params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
                if (value == values[i]) return true;
            return false;
        }
        private static bool IsOpen(string text)
        {
            return OneOf(text, "(", "[", "[|", "[<", "{");
        }
        private static bool IsClose(string text)
        {
            return OneOf(text, ")", "]", "|]", ">]", "}");
        }
        private static bool Matches(string open, string close)
        {
            return open == "(" && close == ")" || open == "[" && close == "]" ||
                open == "[|" && close == "|]" || open == "[<" && close == ">]" ||
                open == "{" && close == "}";
        }
    }
}
