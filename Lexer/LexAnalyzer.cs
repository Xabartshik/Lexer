// File: Lexer.cs
using System;
using System.Collections.Generic;
using System.Text;

namespace Lexer
{
    // Профиль языка управляет набором распознаваемых токенов и правил.
    public enum LanguageProfile { Variant14, Cpp }

    // Типы токенов. Набор зависит от профиля (Variant14 или C++).
    public enum TokenType
    {
        // Общие токены
        Identifier,        // Идентификатор: [A-Za-z_][A-Za-z0-9_]*
        AssignColonEq,     // Присваивание Variant14: :=
        LParen,            // Левая скобка: (
        RParen,            // Правая скобка: )
        Semicolon,         // Точка с запятой: ;
        Keyword,           // Ключевое слово текущего профиля
        CharConstTF,       // Символьная константа Variant14: 'T' или 'F'

        // Расширенные токены (для профиля C++)
        Number,            // Числовой литерал (целый/вещественный, в т.ч. 0x..., 0b..., с суффиксами)
        StringLiteral,     // Строковый литерал: "..."
        CharLiteral,       // Символьный литерал: 'a', '\n' и т.п.
        Operator,          // Оператор: +, -, ==, +=, &&, ->, :: и др.
        Delimiter,         // Разделитель: (), {}, [], ;, ,
        Preprocessor,      // Директива препроцессора: строка, начинающаяся с '#'
        Comment,           // Комментарий: // ... или /* ... */
        Whitespace,        // (Не используется для генерации токенов — пробелы просто пропускаются)
        Unknown,           // Неизвестный/некорректный символ или фрагмент
        EndOfFile          // Конец входа
    }

    // Токен: тип, исходная лексема и координаты начала в исходнике.
    public record Token(TokenType Type, string Lexeme, int Line, int Column);

    public sealed class LexAnalyzer
    {
        // Входной текст
        private readonly string _src;
        // Абсолютный индекс, строка и столбец
        private int _i, _line = 1, _col = 1;
        // Тип используемого языкового профиля - для лабораторной или для С++ в целом
        private readonly LanguageProfile _profile;
        // Ключевые слова лабораторной (минимальный набор логических выражений)
        private readonly HashSet<string> _kwVariant14 = new(StringComparer.Ordinal)
        { "or", "xor", "and", "not" };
        // Список ошибок, содержащий позицию и тип ошибки.
        private readonly List<(int Line, int Col, string Message)> _errors = new();

        public IReadOnlyList<(int, int, string)> Errors => _errors.AsReadOnly();
        // Подмножество ключевых слов C++ (минимальный список, честно украденный из интернета)
        private readonly HashSet<string> _kwCpp = new(StringComparer.Ordinal)
        {
            // Минимальный практический поднабор
            "alignas","alignof","and","and_eq","asm","auto","bitand","bitor","bool","break",
            "case","catch","char","char8_t","char16_t","char32_t","class","compl","const",
            "constexpr","const_cast","continue","decltype","default","delete","do","double",
            "dynamic_cast","else","enum","explicit","export","extern","false","float","for",
            "friend","goto","if","inline","int","long","mutable","namespace","new","noexcept",
            "not","not_eq","nullptr","operator","or","or_eq","private","protected","public",
            "register","reinterpret_cast","return","short","signed","sizeof","static",
            "static_cast","struct","switch","template","this","thread_local","throw","true",
            "try","typedef","typeid","typename","union","unsigned","using","virtual","void",
            "volatile","wchar_t","while","xor","xor_eq"
        };


        public LexAnalyzer(string source, LanguageProfile profile = LanguageProfile.Variant14)
        {
            _src = source ?? string.Empty;
            _profile = profile;
        }

        //Генератор токенов - Итератор.
        public IEnumerable<Token> Scan()
        {
            while (true)
            {
                var ch = Peek();

                // Конец входа: отдаем специальный токен и прерываем генерацию.
                if (ch == '\0') { yield return new Token(TokenType.EndOfFile, "", _line, _col); yield break; }

                // Пробельные символы: просто пропускаем, не формируя отдельные токены.
                if (char.IsWhiteSpace(ch))
                {
                    ConsumeWhitespace();
                    continue;
                }

                // Комментарии (только для профиля C++)
                if (_profile == LanguageProfile.Cpp && ch == '/')
                {
                    // Однострочный комментарий: // ... до конца строки (тело не сохраняется в лексему)
                    if (Match("//"))
                    {
                        var startCol = _col - 2;
                        var startLine = _line;
                        while (Peek() != '\n' && Peek() != '\0') Advance();
                        yield return new Token(TokenType.Comment, "//", startLine, startCol);
                        continue;
                    }

                    // Многострочный комментарий: /* ... */ (движемся до закрывающей последовательности)
                    if (Match("/*"))
                    {
                        var startCol = _col - 2;
                        var startLine = _line;
                        // Match("*/") потребляет оба символа, поэтому проверяем в условии
                        while (!Match("*/") && Peek() != '\0') Advance();
                        yield return new Token(TokenType.Comment, "/* */", startLine, startCol);
                        continue;
                    }
                }

                // Препроцессор (C++): строка, начинающаяся с '#'
                // IsLineStart учитывает только пробелы слева до ближайшего '\n'.
                if (_profile == LanguageProfile.Cpp && ch == '#' && IsLineStart())
                {
                    var startLine = _line; var startCol = _col;
                    var sb = new StringBuilder();
                    while (Peek() != '\n' && Peek() != '\0') sb.Append(Advance());
                    yield return new Token(TokenType.Preprocessor, sb.ToString(), startLine, startCol);
                    continue;
                }

                // Присваивание Variant14: ':='
                if (ch == ':' && _profile == LanguageProfile.Variant14)
                {
                    var startLine = _line; var startCol = _col;
                    Advance(); // съедаем ':'
                    if (Peek() == '=')
                    {
                        Advance(); // съедаем '='
                        yield return new Token(TokenType.AssignColonEq, ":=", startLine, startCol);
                        continue;
                    }

                    // Одиночное ':' в Variant14 не определено — помечаем как Unknown.
                    _errors.Add((_line, _col, $"Символ, който не е включен в граматиката LR4B14: ':'"));
                    yield return new Token(TokenType.Unknown, ":", startLine, startCol);
                    continue;
                }

                // Простые разделители (общие для обоих профилей)
                if (ch == '(') { yield return EmitSingle(TokenType.LParen); continue; }
                if (ch == ')') { yield return EmitSingle(TokenType.RParen); continue; }
                if (ch == ';') { yield return EmitSingle(TokenType.Semicolon); continue; }

                // Идентификаторы и ключевые слова
                if (IsIdentStart(ch))
                {
                    var start = Mark();
                    Advance(); // первый символ уже проверен
                    while (IsIdentPart(Peek())) Advance();
                    var lex = Slice(start);

                    // Классификация по набору ключевых слов активного профиля
                    if (_profile == LanguageProfile.Variant14 && _kwVariant14.Contains(lex))
                        yield return new Token(TokenType.Keyword, lex, start.line, start.col);
                    else if (_profile == LanguageProfile.Cpp && _kwCpp.Contains(lex))
                        yield return new Token(TokenType.Keyword, lex, start.line, start.col);
                    else
                        yield return new Token(TokenType.Identifier, lex, start.line, start.col);
                    continue;
                }

                // Символьные константы Variant14: строго 'T' или 'F'
                if (_profile == LanguageProfile.Variant14 && ch == '\'')
                {
                    var start = Mark();
                    Advance(); // открывающая '

                    var mid = Peek();
                    // Проверяем форму 'T' или 'F': один символ и закрывающая кавычка.
                    if ((mid == 'T' || mid == 'F') && LookAhead(1) == '\'')
                    {
                        Advance(); // T/F
                        Advance(); // закрывающая '
                        var lex = "'" + mid + "'";
                        yield return new Token(TokenType.CharConstTF, lex, start.line, start.col);
                        continue;
                    }
                    _errors.Add((_line, _col, $"Символ, който не е включен в граматиката LR4B14"));
                    // Некорректная символьная константа: проматываем до следующей кавычки/перевода строки/EOF.
                    while (Peek() != '\'' && Peek() != '\0' && Peek() != '\n') Advance();
                    if (Peek() == '\'') Advance();
                    yield return new Token(TokenType.Unknown, "", start.line, start.col);
                    continue;
                }

                // Литералы C++: строки и символы с экранированием
                if (_profile == LanguageProfile.Cpp && (ch == '"' || ch == '\''))
                {
                    var tok = ScanStringOrChar();
                    yield return tok;
                    continue;
                }

                // Числа C++ (упрощённый охват)
                // Поддерживает: 0x... (hex), 0b... (bin), десятичные, точку, экспоненту, суффиксы uUlLfF и разделитель '_'
                if (_profile == LanguageProfile.Cpp && (char.IsDigit(ch) || (ch == '.' && char.IsDigit(LookAhead(1)))))
                {
                    var tok = ScanNumber();
                    yield return tok;
                    continue;
                }

                // Операторы и разделители (C++): много- и односивольные последовательности
                if (_profile == LanguageProfile.Cpp)
                {
                    var op = ScanOperatorOrDelimiter();
                    if (op.Type != TokenType.Unknown) { yield return op; continue; }
                }

                // Запасной вариант: неизвестный символ — возвращаем Unknown и продвигаемся.
                var unknown = EmitSingle(TokenType.Unknown);
                _errors.Add((_line, _col, $"Неизвестный символ: '{unknown.Lexeme}'"));
                yield return unknown;
            }
        }

        //Текущие координаты лексера
        private (int pos, int line, int col) Mark() => (_i, _line, _col);
        //Возвращает подстроку от позиции до текущего абсиндекса
        private string Slice((int pos, int line, int col) m) => _src[m.pos.._i];
        //Возвращает подстроку от указанной позиции, до конечной (не абсиндекса)
        private string SliceRange(int startPos, int endPos) => _src.Substring(startPos, endPos - startPos); 

        //Посмотреть символ без сдвига по абсиндексу 
        private char Peek() => _i < _src.Length ? _src[_i] : '\0';
        //Просмотр на несколько символов вперед относительно абсиндекса
        private char LookAhead(int k) => (_i + k) < _src.Length ? _src[_i + k] : '\0';
        //Продвинуться по лексеру
        private char Advance()
        {
            char c = Peek();
            if (c == '\0') return '\0';
            _i++;
            if (c == '\n') { _line++; _col = 1; } else { _col++; }
            return c;
        }
        //Совпадает ли подстроке s текущее положение абсиндекса
        private bool Match(string s)
        {
            for (int k = 0; k < s.Length; k++)
                if (LookAhead(k) != s[k]) return false;
            for (int k = 0; k < s.Length; k++) Advance();
            return true;
        }
        //Токен из одного символа. Нужен для обработки всякой частой мелочи 
        private Token EmitSingle(TokenType type)
        {
            var line = _line; var col = _col; var c = Advance();
            return new Token(type, c.ToString(), line, col);
        }
        //Пропуск пробелов и табов
        private void ConsumeWhitespace()
        {
            while (char.IsWhiteSpace(Peek())) Advance();
        }
        //Проверяет, начало ли строки
        private bool IsLineStart()
        {
            if (_i == 0) return true;
            int j = _i - 1;
            while (j >= 0 && _src[j] != '\n') { if (!char.IsWhiteSpace(_src[j])) return false; j--; }
            return true;
        }

        private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_';
        private static bool IsIdentPart(char c) => char.IsLetterOrDigit(c) || c == '_';

        //Считывает строку и/или символ
        private Token ScanStringOrChar()
        {
            var startLine = _line; var startCol = _col; var quote = Advance(); // ' or "
            var sb = new StringBuilder().Append(quote);
            bool isEsc = false;
            while (true)
            {
                var c = Peek();
                if (c == '\0' || c == '\n') break;
                sb.Append(Advance());
                if (isEsc) { isEsc = false; continue; }
                if (c == '\\') { isEsc = true; continue; }
                if (c == quote) break;
            }
            var lex = sb.ToString();
            var type = quote == '"' ? TokenType.StringLiteral : TokenType.CharLiteral;
            return new Token(type, lex, startLine, startCol);
        }

        private Token ScanNumber()
        {
            var start = Mark();

            // Обработка шестнадцатеричных чисел (0x...)
            if (Match("0x") || Match("0X"))
            {
                if (!Uri.IsHexDigit(Peek()))
                {
                    _errors.Add((_line, _col,
                        "Неправильное шестнадцатеричной число: нет чисел после '0x'"));
                    
                }
                while (Uri.IsHexDigit(Peek())) Advance();
            }
            // Обработка двоичных чисел (0b...)
            else if (Match("0b") || Match("0B"))
            {
                if (Peek() != '0' && Peek() != '1')
                {
                    _errors.Add((_line, _col,
                        "Неправильное двоичное число: нет чисел после '0b'"));
                    
                }
                while (Peek() == '0' || Peek() == '1' || Peek() == '\'') Advance();
            }
            // Обработка десятичных чисел (с поддержкой точки, экспоненты, разделителей)
            else
            {
                // Целая часть
                while (char.IsDigit(Peek()) || Peek() == '\'') Advance();
                // Дробная часть
                if (Peek() == '.')
                {
                    Advance(); // первая точка
                    if (!char.IsDigit(Peek()))
                    {
                        _errors.Add((_line, _col, "Неправильное число: ожидалась дробная часть"));
                    }
                    while (char.IsDigit(Peek()) || Peek() == '\'') Advance();
                }

                // Экспонента
                if (Peek() == 'e' || Peek() == 'E')
                {
                    Advance();
                    if (Peek() == '+' || Peek() == '-') Advance();
                    if (!char.IsDigit(Peek()))
                    {
                        _errors.Add((_line, _col, "Неправильная экспонента: ожидались цифры после 'E'"));
                    }
                    while (char.IsDigit(Peek())) Advance();
                }

                // Сохраняем позицию конца нормального числа
                int endValid = _i;

                // Обнаружение лишних точек после точки
                if (Peek() == '.' && char.IsDigit(LookAhead(1)))
                {
                    int errLine = _line, errCol = _col; // позиция первой лишней точки

                    // Проглатываем один или несколько сегментов ". + цифры/'"
                    do
                    {
                        Advance(); // '.'
                        while (char.IsDigit(Peek()) || Peek() == '\'') Advance();
                    }
                    while (Peek() == '.' && char.IsDigit(LookAhead(1)));

                    _errors.Add((errLine, errCol, "Неправильное число: больше одной точки (использована только первая)"));

                    // Возвращаем обрезанную лексему "12.2"
                    var fixedLex = SliceRange(start.pos, endValid);
                    return new Token(TokenType.Number, fixedLex, start.line, start.col);
                }
            }
            // Парсинг суффиксов (u, U, l, L, f, F)
            var suffixStart = Mark();
            var suffixSb = new StringBuilder();
            while ("uUlLfF".IndexOf(Peek()) >= 0)
            {
                suffixSb.Append(Advance());
            }
            var suffix = suffixSb.ToString();
            bool hasIntSuffix = suffix.IndexOfAny(new[] { 'u', 'U', 'l', 'L' }) >= 0;
            bool hasFloatSuffix = suffix.IndexOfAny(new[] { 'f', 'F' }) >= 0;

            if (hasIntSuffix && hasFloatSuffix)
            {
                _errors.Add((_line, _col,
                    $"Конфликтующие суффиксы: '{suffix}' (нельзя смешивать суффиксы для целых чисел и чисел с плавающей точкой)"));
                
            }
            var numLexeme = Slice(start);
            return new Token(TokenType.Number, numLexeme, start.line, start.col);
        }


        private Token ScanOperatorOrDelimiter()
        {
            var start = Mark();
            string[] two = { "==", "!=", "<=", ">=", "++", "--", "&&", "||", "<<", ">>", "->", "::", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=" };
            foreach (var t in two)
            {
                if (Match(t)) return new Token(TokenType.Operator, t, start.line, start.col);
            }
            char c = Peek();
            string oneSet = "+-*/%<>=!&|^~?:.,[]{}()';\\@#";
            if (oneSet.IndexOf(c) >= 0)
            {
                Advance();
                var ttype = "()".IndexOf(c) >= 0 ? TokenType.Delimiter :
                            ";,".IndexOf(c) >= 0 ? TokenType.Delimiter : TokenType.Operator;
                return new Token(ttype, c.ToString(), start.line, start.col);
            }
            return new Token(TokenType.Unknown, Advance().ToString(), start.line, start.col);
        }
    }
}
