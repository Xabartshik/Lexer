// File: LexAnalyzer.cs
using System;
using System.Collections.Generic;
using System.Text;

namespace Lexer;

/// <summary>
/// Профиль языка управляет набором распознаваемых токенов.
/// - Variant14: Логические выражения + расширенные конструкции (if/while/for)
/// - Cpp: Полноценный C++ с типами, классами, функциями
/// </summary>
public enum LanguageProfile { Variant14, Cpp }

/// <summary>
/// Типы токенов - расширенный набор для обеих профилей
/// </summary>
public enum TokenType
{
    // ═══════════════════════════════════════════════
    // Базовые токены
    // ═══════════════════════════════════════════════

    Identifier,           // [a-zA-Z_][a-zA-Z0-9_]*
    Number,              // 123, 3.14, 0xFF, 0b101
    StringLiteral,       // "hello world"
    CharLiteral,         // 'a', '\n'
    CharConstTF,         // Variant14: 'T' или 'F'

    // ═══════════════════════════════════════════════
    // Ключевые слова
    // ═══════════════════════════════════════════════

    Keyword,             // or, and, not, if, else, for, while, etc.

    // ═══════════════════════════════════════════════
    // Операторы и разделители
    // ═══════════════════════════════════════════════

    Operator,            // +, -, *, /, ==, !=, >=, <=, &&, ||, etc.
    AssignColonEq,       // := (Variant14)
    LParen,              // (
    RParen,              // )
    LBrace,              // {
    RBrace,              // }
    LBracket,            // [
    RBracket,            // ]
    Semicolon,           // ;
    Comma,               // ,
    Dot,                 // .
    Colon,               // :
    Arrow,               // -> (C++)
    DoubleColon,         // :: (C++)

    // ═══════════════════════════════════════════════
    // Служебные токены
    // ═══════════════════════════════════════════════

    Preprocessor,        // #include, #define (C++)
    Comment,             // // или /* */ (пропускается)
    Whitespace,          // пробелы (пропускается)
    EndOfFile,
    Unknown
}

/// <summary>
/// Токен - минимальная лексическая единица
/// </summary>
public record Token(TokenType Type, string Lexeme, int Line, int Column);

/// <summary>
/// Лексический анализатор - преобразует исходный код в токены
/// </summary>
public sealed class LexAnalyzer
{
    private readonly string _input;
    private readonly LanguageProfile _profile;
    private int _pos = 0;
    private int _line = 1;
    private int _column = 1;

    private const char EOF_CHAR = '\0';

    public List<(int Line, int Col, string Message)> Errors { get; } = new();

    public LexAnalyzer(string input, LanguageProfile profile)
    {
        _input = input ?? string.Empty;
        _profile = profile;
    }

    public List<Token> Scan()
    {
        var tokens = new List<Token>();

        while (_pos < _input.Length)
        {
            var token = ScanToken();
            if (token.Type != TokenType.Unknown)
                tokens.Add(token);
        }

        tokens.Add(new Token(TokenType.EndOfFile, string.Empty, _line, _column));
        return tokens;
    }

    private Token ScanToken()
    {
        SkipWhitespace();

        if (_pos >= _input.Length)
            return new Token(TokenType.EndOfFile, string.Empty, _line, _column);

        int startLine = _line;
        int startCol = _column;
        char ch = _input[_pos];

        // ═══════════════════════════════════════════════
        // Комментарии и препроцессор
        // ═══════════════════════════════════════════════

        if (ch == '/' && PeekNext() == '/')
        {
            SkipLineComment();
            return ScanToken();  // Рекурсивно продолжаем
        }

        if (ch == '/' && PeekNext() == '*')
        {
            SkipBlockComment();
            return ScanToken();
        }

        if (ch == '#')
        {
            return ScanPreprocessor();
        }

        // ═══════════════════════════════════════════════
        // Числовые литералы
        // ═══════════════════════════════════════════════

        if (char.IsDigit(ch))
        {
            return ScanNumber();
        }

        // ═══════════════════════════════════════════════
        // Строковые и символьные литералы
        // ═══════════════════════════════════════════════

        if (ch == '"')
        {
            return ScanStringLiteral();
        }

        if (ch == '\'')
        {
            return ScanCharLiteral();
        }

        // ═══════════════════════════════════════════════
        // Идентификаторы и ключевые слова
        // ═══════════════════════════════════════════════

        if (char.IsLetter(ch) || ch == '_')
        {
            return ScanIdentifierOrKeyword();
        }

        // ═══════════════════════════════════════════════
        // Операторы и разделители
        // ═══════════════════════════════════════════════

        return ScanOperatorOrDelimiter();
    }

    private void SkipWhitespace()
    {
        while (_pos < _input.Length && char.IsWhiteSpace(_input[_pos]))
        {
            if (_input[_pos] == '\n')
            {
                _line++;
                _column = 1;
            }
            else
            {
                _column++;
            }
            _pos++;
        }
    }

    private char Current =>
        _pos < _input.Length ? _input[_pos] : EOF_CHAR;

    private char PeekNext() =>
        _pos + 1 < _input.Length ? _input[_pos + 1] : EOF_CHAR;

    private char PeekAhead(int offset) =>
        _pos + offset < _input.Length ? _input[_pos + offset] : EOF_CHAR;

    private void Advance()
    {
        if (_pos < _input.Length)
        {
            if (_input[_pos] == '\n')
            {
                _line++;
                _column = 1;
            }
            else
            {
                _column++;
            }
            _pos++;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // КОММЕНТАРИИ
    // ═══════════════════════════════════════════════════════════

    private void SkipLineComment()
    {
        Advance(); // /
        Advance(); // /
        while (_pos < _input.Length && _input[_pos] != '\n')
            Advance();
    }

    private void SkipBlockComment()
    {
        Advance(); // /
        Advance(); // *
        while (_pos < _input.Length)
        {
            if (_input[_pos] == '*' && PeekNext() == '/')
            {
                Advance(); // *
                Advance(); // /
                break;
            }
            Advance();
        }
    }

    // ═══════════════════════════════════════════════════════════
    // ПРЕПРОЦЕССОР
    // ═══════════════════════════════════════════════════════════

    private Token ScanPreprocessor()
    {
        int startLine = _line;
        int startCol = _column;
        var sb = new StringBuilder();

        while (_pos < _input.Length && _input[_pos] != '\n')
        {
            sb.Append(_input[_pos]);
            Advance();
        }

        return new Token(TokenType.Preprocessor, sb.ToString(), startLine, startCol);
    }

    // ═══════════════════════════════════════════════════════════
    // ЧИСЛОВЫЕ ЛИТЕРАЛЫ
    // ═══════════════════════════════════════════════════════════

    private Token ScanNumber()
    {
        int startLine = _line;
        int startCol = _column;
        var sb = new StringBuilder();

        // Шестнадцатеричные (0xFF), двоичные (0b101)
        if (Current == '0' && (PeekNext() == 'x' || PeekNext() == 'X'))
        {
            sb.Append(Current);
            Advance();
            sb.Append(Current);
            Advance();
            while (_pos < _input.Length && IsHexDigit(Current))
            {
                sb.Append(Current);
                Advance();
            }
            return new Token(TokenType.Number, sb.ToString(), startLine, startCol);
        }

        if (Current == '0' && (PeekNext() == 'b' || PeekNext() == 'B'))
        {
            sb.Append(Current);
            Advance();
            sb.Append(Current);
            Advance();
            while (_pos < _input.Length && (Current == '0' || Current == '1'))
            {
                sb.Append(Current);
                Advance();
            }
            return new Token(TokenType.Number, sb.ToString(), startLine, startCol);
        }

        // Десятичные числа
        while (_pos < _input.Length && char.IsDigit(Current))
        {
            sb.Append(Current);
            Advance();
        }

        // Вещественные числа
        if (Current == '.' && char.IsDigit(PeekNext()))
        {
            sb.Append(Current);
            Advance();
            while (_pos < _input.Length && char.IsDigit(Current))
            {
                sb.Append(Current);
                Advance();
            }
        }

        // Суффиксы (f, l, u, ll, ul и т.д.)
        while (_pos < _input.Length && (char.IsLetter(Current) || Current == '_'))
        {
            sb.Append(Current);
            Advance();
        }

        return new Token(TokenType.Number, sb.ToString(), startLine, startCol);
    }

    private bool IsHexDigit(char ch)
    {
        return char.IsDigit(ch) || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F');
    }

    // ═══════════════════════════════════════════════════════════
    // СТРОКОВЫЕ ЛИТЕРАЛЫ
    // ═══════════════════════════════════════════════════════════

    private Token ScanStringLiteral()
    {
        int startLine = _line;
        int startCol = _column;
        var sb = new StringBuilder();

        sb.Append(Current);  // "
        Advance();

        while (_pos < _input.Length && Current != '"')
        {
            if (Current == '\\')
            {
                sb.Append(Current);
                Advance();
                if (_pos < _input.Length)
                {
                    sb.Append(Current);
                    Advance();
                }
            }
            else
            {
                sb.Append(Current);
                Advance();
            }
        }

        if (Current == '"')
        {
            sb.Append(Current);
            Advance();
        }
        else
        {
            Errors.Add((_line, _column, "Незакрытая строка"));
        }

        return new Token(TokenType.StringLiteral, sb.ToString(), startLine, startCol);
    }

    // ═══════════════════════════════════════════════════════════
    // СИМВОЛЬНЫЕ ЛИТЕРАЛЫ
    // ═══════════════════════════════════════════════════════════

    private Token ScanCharLiteral()
    {
        int startLine = _line;
        int startCol = _column;
        var sb = new StringBuilder();

        sb.Append(Current);  // '
        Advance();

        // Для Variant14: только 'T' или 'F'
        if (_profile == LanguageProfile.Variant14 && (Current == 'T' || Current == 'F'))
        {
            sb.Append(Current);
            Advance();
            if (Current == '\'')
            {
                sb.Append(Current);
                Advance();
                return new Token(TokenType.CharConstTF, sb.ToString(), startLine, startCol);
            }
        }

        // Для C++: любые символы с экранированием
        if (Current == '\\')
        {
            sb.Append(Current);
            Advance();
            if (_pos < _input.Length)
            {
                sb.Append(Current);
                Advance();
            }
        }
        else if (Current != '\'')
        {
            sb.Append(Current);
            Advance();
        }

        if (Current == '\'')
        {
            sb.Append(Current);
            Advance();
        }
        else
        {
            Errors.Add((_line, _column, "Незакрытый символьный литерал"));
        }

        return new Token(TokenType.CharLiteral, sb.ToString(), startLine, startCol);
    }

    // ═══════════════════════════════════════════════════════════
    // ИДЕНТИФИКАТОРЫ И КЛЮЧЕВЫЕ СЛОВА
    // ═══════════════════════════════════════════════════════════

    private Token ScanIdentifierOrKeyword()
    {
        int startLine = _line;
        int startCol = _column;
        var sb = new StringBuilder();

        while (_pos < _input.Length && (char.IsLetterOrDigit(Current) || Current == '_'))
        {
            sb.Append(Current);
            Advance();
        }

        string lexeme = sb.ToString();

        // Проверка ключевых слов
        if (IsKeyword(lexeme))
        {
            return new Token(TokenType.Keyword, lexeme, startLine, startCol);
        }

        return new Token(TokenType.Identifier, lexeme, startLine, startCol);
    }

    private bool IsKeyword(string word)
    {
        var v14Keywords = new HashSet<string>
        {
            "or", "and", "not", "xor", "true", "false", "T", "F"
        };

        var cppKeywords = new HashSet<string>
        {
            // Типы данных
            "int", "float", "double", "char", "bool", "void", "long", "short",
            "unsigned", "signed", "auto", "const", "static", "volatile",

            // Управление потоком
            "if", "else", "switch", "case", "default", "break", "continue",
            "for", "while", "do", "return", "goto",

            // Логические
            "true", "false", "and", "or", "not", "xor", "&&", "||", "!",

            // Другое
            "struct", "class", "union", "enum", "namespace", "using", "new",
            "delete", "template", "typename", "public", "private", "protected"
        };

        if (_profile == LanguageProfile.Variant14)
            return v14Keywords.Contains(word);
        else
            return cppKeywords.Contains(word);
    }

    // ═══════════════════════════════════════════════════════════
    // ОПЕРАТОРЫ И РАЗДЕЛИТЕЛИ
    // ═══════════════════════════════════════════════════════════

    private Token ScanOperatorOrDelimiter()
    {
        int startLine = _line;
        int startCol = _column;
        char ch = Current;

        // Двусимвольные операторы
        if (ch == ':' && PeekNext() == '=')
        {
            Advance();
            Advance();
            return new Token(TokenType.AssignColonEq, ":=", startLine, startCol);
        }

        if (ch == '=' && PeekNext() == '=')
        {
            Advance();
            Advance();
            return new Token(TokenType.Operator, "==", startLine, startCol);
        }

        if (ch == '!' && PeekNext() == '=')
        {
            Advance();
            Advance();
            return new Token(TokenType.Operator, "!=", startLine, startCol);
        }

        if (ch == '<' && PeekNext() == '=')
        {
            Advance();
            Advance();
            return new Token(TokenType.Operator, "<=", startLine, startCol);
        }

        if (ch == '>' && PeekNext() == '=')
        {
            Advance();
            Advance();
            return new Token(TokenType.Operator, ">=", startLine, startCol);
        }

        if (ch == '&' && PeekNext() == '&')
        {
            Advance();
            Advance();
            return new Token(TokenType.Operator, "&&", startLine, startCol);
        }

        if (ch == '|' && PeekNext() == '|')
        {
            Advance();
            Advance();
            return new Token(TokenType.Operator, "||", startLine, startCol);
        }

        if (ch == '+' && PeekNext() == '+')
        {
            Advance();
            Advance();
            return new Token(TokenType.Operator, "++", startLine, startCol);
        }

        if (ch == '-' && PeekNext() == '-')
        {
            Advance();
            Advance();
            return new Token(TokenType.Operator, "--", startLine, startCol);
        }

        if (ch == '-' && PeekNext() == '>')
        {
            Advance();
            Advance();
            return new Token(TokenType.Arrow, "->", startLine, startCol);
        }

        if (ch == ':' && PeekNext() == ':')
        {
            Advance();
            Advance();
            return new Token(TokenType.DoubleColon, "::", startLine, startCol);
        }

        if (ch == '<' && PeekNext() == '<')
        {
            Advance();
            Advance();
            return new Token(TokenType.Operator, "<<", startLine, startCol);
        }

        if (ch == '>' && PeekNext() == '>')
        {
            Advance();
            Advance();
            return new Token(TokenType.Operator, ">>", startLine, startCol);
        }

        // Однозначные операторы и разделители
        Advance();

        return ch switch
        {
            '(' => new Token(TokenType.LParen, "(", startLine, startCol),
            ')' => new Token(TokenType.RParen, ")", startLine, startCol),
            '{' => new Token(TokenType.LBrace, "{", startLine, startCol),
            '}' => new Token(TokenType.RBrace, "}", startLine, startCol),
            '[' => new Token(TokenType.LBracket, "[", startLine, startCol),
            ']' => new Token(TokenType.RBracket, "]", startLine, startCol),
            ';' => new Token(TokenType.Semicolon, ";", startLine, startCol),
            ',' => new Token(TokenType.Comma, ",", startLine, startCol),
            '.' => new Token(TokenType.Dot, ".", startLine, startCol),
            ':' => new Token(TokenType.Colon, ":", startLine, startCol),
            '+' => new Token(TokenType.Operator, "+", startLine, startCol),
            '-' => new Token(TokenType.Operator, "-", startLine, startCol),
            '*' => new Token(TokenType.Operator, "*", startLine, startCol),
            '/' => new Token(TokenType.Operator, "/", startLine, startCol),
            '%' => new Token(TokenType.Operator, "%", startLine, startCol),
            '=' => new Token(TokenType.Operator, "=", startLine, startCol),
            '<' => new Token(TokenType.Operator, "<", startLine, startCol),
            '>' => new Token(TokenType.Operator, ">", startLine, startCol),
            '!' => new Token(TokenType.Operator, "!", startLine, startCol),
            '&' => new Token(TokenType.Operator, "&", startLine, startCol),
            '|' => new Token(TokenType.Operator, "|", startLine, startCol),
            '^' => new Token(TokenType.Operator, "^", startLine, startCol),
            '~' => new Token(TokenType.Operator, "~", startLine, startCol),
            '?' => new Token(TokenType.Operator, "?", startLine, startCol),
            _ => new Token(TokenType.Unknown, ch.ToString(), startLine, startCol)
        };
    }
}
