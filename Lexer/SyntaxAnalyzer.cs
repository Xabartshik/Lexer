// File: SyntaxAnalyzer.cs - расширенный вариант с поддержкой Variant14 и упрощённого C++
using System;
using System.Collections.Generic;
using System.Linq;
using Lexer;

namespace Parser;

public sealed class SyntaxAnalyzer
{
    private readonly List<Token> _tokenList;
    private int _pos;
    private readonly LanguageProfile _profile;
    private Token _lookahead;

    private bool _inErrorRecovery = false;

    public List<(int Line, int Col, string Message)> Errors { get; } = new();

    public SyntaxAnalyzer(IEnumerable<Token> tokens, LanguageProfile profile)
    {
        if (tokens == null) throw new ArgumentNullException(nameof(tokens));

        _tokenList = tokens
            .Where(t => t.Type != TokenType.Whitespace && t.Type != TokenType.Comment)
            .ToList();

        _profile = profile;
        _pos = 0;
        _lookahead = _pos < _tokenList.Count
            ? _tokenList[_pos]
            : new Token(TokenType.EndOfFile, string.Empty, -1, -1);
    }

    private void MoveNext()
    {
        if (_pos < _tokenList.Count)
            _pos++;

        _lookahead = _pos < _tokenList.Count
            ? _tokenList[_pos]
            : new Token(TokenType.EndOfFile, string.Empty, -1, -1);
    }

    private bool Is(TokenType type, string? lexeme = null) =>
        _lookahead.Type == type &&
        (lexeme == null || string.Equals(_lookahead.Lexeme, lexeme, StringComparison.Ordinal));

    private Token Consume()
    {
        var t = _lookahead;
        MoveNext();
        return t;
    }

    private Token Expect(TokenType type, string? lexeme, string message)
    {
        if (Is(type, lexeme))
            return Consume();

        if (!_inErrorRecovery)
        {
            _inErrorRecovery = true;
            Errors.Add((_lookahead.Line, _lookahead.Column, message));
        }

        return _lookahead;
    }

    // =====================================================================
    // ТОЧКА ВХОДА
    // =====================================================================

    public ProgramNode? ParseProgram()
    {
        try
        {
            return _profile switch
            {
                LanguageProfile.Variant14 => ParseV14Program(),
                LanguageProfile.Cpp => ParseCppProgram(),
                _ => throw new NotSupportedException($"Профиль {_profile} не поддерживается.")
            };
        }
        catch (Exception ex)
        {
            Errors.Add((-1, -1, $"Критическая ошибка парсера: {ex.Message}"));
            return null;
        }
    }

    // =====================================================================
    // VARIANT14
    // =====================================================================

    private ProgramNode ParseV14Program()
    {
        var stmts = new List<AstNode>();

        while (_lookahead.Type != TokenType.EndOfFile)
        {
            if (_lookahead.Type == TokenType.Semicolon)
            {
                Consume();
                continue;
            }

            var stmt = ParseV14Statement();
            if (stmt != null)
            {
                stmts.Add(stmt);
                _inErrorRecovery = false;
            }
            else if (_lookahead.Type != TokenType.EndOfFile)
            {
                _inErrorRecovery = false;
            }
        }

        return new ProgramNode(stmts);
    }

    private AstNode? ParseV14Statement()
    {
        if (_lookahead.Type != TokenType.Identifier)
        {
            Errors.Add((_lookahead.Line, _lookahead.Column,
                "Ожидался идентификатор в начале оператора присваивания (Variant14)."));
            SkipToSemicolon();
            return null;
        }

        var idTok = Consume();

        if (!Is(TokenType.AssignColonEq))
        {
            Errors.Add((_lookahead.Line, _lookahead.Column,
                "Ожидался оператор ':=' после идентификатора."));
            SkipToSemicolon();
            return null;
        }

        Consume(); // ':='
        var expr = ParseV14Expr();

        if (!Is(TokenType.Semicolon))
        {
            Errors.Add((_lookahead.Line, _lookahead.Column,
                "Ожидалась ';' в конце оператора."));
            SkipToSemicolon();
        }
        else
        {
            Consume();
        }

        var left = new IdentifierNode(idTok.Lexeme, idTok.Line, idTok.Column);
        return new AssignNode(left, ":=", expr);
    }

    private AstNode ParseV14Expr()
    {
        var left = ParseV14Term();

        while (Is(TokenType.Keyword, "or") || Is(TokenType.Keyword, "xor"))
        {
            var opTok = Consume();
            var right = ParseV14Term();
            left = new BinaryNode(opTok.Lexeme, left, right);
        }

        return left;
    }

    private AstNode ParseV14Term()
    {
        var left = ParseV14Factor();

        while (Is(TokenType.Keyword, "and"))
        {
            var opTok = Consume();
            var right = ParseV14Factor();
            left = new BinaryNode(opTok.Lexeme, left, right);
        }

        return left;
    }

    private AstNode ParseV14Factor()
    {
        if (Is(TokenType.Keyword, "not"))
        {
            var op = Consume();
            var operand = ParseV14Factor();
            return new UnaryNode(op.Lexeme, operand);
        }

        return ParseV14Primary();
    }

    private AstNode ParseV14Primary()
    {
        if (_lookahead.Type == TokenType.Identifier)
        {
            var t = Consume();
            return new IdentifierNode(t.Lexeme, t.Line, t.Column);
        }

        if (_lookahead.Type == TokenType.CharConstTF)
        {
            var t = Consume();
            return new LiteralNode("char", t.Lexeme, t.Line, t.Column);
        }

        if (_lookahead.Type == TokenType.LParen)
        {
            Consume();
            var expr = ParseV14Expr();
            if (!Is(TokenType.RParen))
                Errors.Add((_lookahead.Line, _lookahead.Column,
                    "Ожидалась закрывающая скобка ')' в выражении."));
            else
                Consume();
            return expr;
        }

        Errors.Add((_lookahead.Line, _lookahead.Column,
            $"Ожидалось логическое выражение (Variant14), получено '{_lookahead.Lexeme}'"));
        var errTok = Consume();
        return new LiteralNode("error", errTok.Lexeme, errTok.Line, errTok.Column);
    }

    // =====================================================================
    // C++ ПРОФИЛЬ
    // =====================================================================

    private ProgramNode ParseCppProgram()
    {
        var stmts = new List<AstNode>();

        while (_lookahead.Type != TokenType.EndOfFile)
        {
            if (_lookahead.Type == TokenType.Preprocessor)
            {
                Consume();
                continue;
            }

            if (Is(TokenType.Keyword, "using"))
            {
                Consume();
                while (_lookahead.Type != TokenType.Semicolon &&
                       _lookahead.Type != TokenType.EndOfFile)
                {
                    Consume();
                }
                if (Is(TokenType.Semicolon))
                    Consume();
                continue;
            }

            if (_lookahead.Type == TokenType.Semicolon)
            {
                Consume();
                continue;
            }

            // ключевой момент: если встретили }, это ошибка на верхнем уровне - пропускаем
            if (_lookahead.Type == TokenType.RBrace)
            {
                Consume();
                continue;
            }

            var stmt = ParseCppStatementOrFunction();
            if (stmt != null)
            {
                stmts.Add(stmt);
                _inErrorRecovery = false;
            }
            else if (_lookahead.Type != TokenType.EndOfFile)
            {
                _inErrorRecovery = false;
            }
        }

        return new ProgramNode(stmts);
    }

    private AstNode? ParseCppStatementOrFunction()
    {
        if (IsCppType(_lookahead))
        {
            var typeTok = Consume();

            // поддержка шаблонных типов
            SkipCppTemplateArguments();

            // КЛЮЧЕВОЙ ФИ ИСПРАВЛЕНИЕ: проверяем, есть ли вообще идентификатор
            if (_lookahead.Type != TokenType.Identifier)
            {
                // если нет идентификатора, это может быть:
                // 1. конец блока }
                // 2. конец файла
                // 3. ; на верхнем уровне
                // просто пропускаем и возвращаем null
                return null;
            }

            var nameTok = Consume();

            if (Is(TokenType.LParen))
            {
                return ParseCppFunctionDeclaration(typeTok, nameTok);
            }

            return ParseCppVarDeclTail(typeTok, nameTok);
        }

        return ParseCppStatement();
    }

    // ---------- объявления/функции ----------

    private bool IsCppType(Token tok)
    {
        if (tok.Type == TokenType.Keyword)
        {
            string[] types =
            {
                "int", "float", "double", "char", "bool", "void",
                "long", "short", "unsigned", "signed", "auto"
            };
            if (Array.Exists(types, t => t == tok.Lexeme))
                return true;
        }

        if (tok.Type == TokenType.Identifier && tok.Lexeme == "vector")
            return true;

        return false;
    }

    private void SkipCppTemplateArguments()
    {
        if (!Is(TokenType.Operator, "<"))
            return;

        int depth = 0;
        while (_lookahead.Type != TokenType.EndOfFile)
        {
            if (Is(TokenType.Operator, "<"))
            {
                depth++;
                Consume();
            }
            else if (Is(TokenType.Operator, ">"))
            {
                depth--;
                Consume();
                if (depth == 0)
                    break;
            }
            else
            {
                Consume();
            }
        }
    }

    private AstNode ParseCppVarDeclTail(Token typeTok, Token nameTok)
    {
        var idNode = new IdentifierNode(nameTok.Lexeme, nameTok.Line, nameTok.Column);
        var typeNode = new IdentifierNode(typeTok.Lexeme, typeTok.Line, typeTok.Column);

        if (Is(TokenType.Operator, "="))
        {
            Consume();
            var init = ParseCppExpr();
            if (!Is(TokenType.Semicolon))
                Errors.Add((_lookahead.Line, _lookahead.Column,
                    "Ожидалась ';' после инициализации."));
            else
                Consume();
            var assign = new AssignNode(idNode, "=", init);
            return new BinaryNode("decl", typeNode, assign);
        }

        if (!Is(TokenType.Semicolon))
            Errors.Add((_lookahead.Line, _lookahead.Column,
                "Ожидалась ';' после объявления."));
        else
            Consume();

        return new BinaryNode("decl", typeNode, idNode);
    }

    private AstNode ParseCppFunctionDeclaration(Token typeToken, Token nameToken)
    {
        Consume(); // '('
        int depth = 1;
        while (_lookahead.Type != TokenType.EndOfFile && depth > 0)
        {
            if (_lookahead.Type == TokenType.LParen) depth++;
            else if (_lookahead.Type == TokenType.RParen) depth--;
            if (depth > 0) Consume();
        }

        if (!Is(TokenType.RParen))
        {
            Errors.Add((_lookahead.Line, _lookahead.Column,
                "Ожидалась ')' в объявлении функции."));
            SkipToSemicolon();
            return new LiteralNode("error", nameToken.Lexeme, nameToken.Line, nameToken.Column);
        }

        Consume();

        var typeNode = new IdentifierNode(typeToken.Lexeme, typeToken.Line, typeToken.Column);
        var nameNode = new IdentifierNode(nameToken.Lexeme, nameToken.Line, nameToken.Column);

        if (Is(TokenType.LBrace))
        {
            var body = ParseCppBlock();
            return new BinaryNode("func-def", typeNode,
                new BinaryNode("func-params-body", nameNode, body));
        }

        if (Is(TokenType.Semicolon))
        {
            Consume();
            return new BinaryNode("func-proto", typeNode, nameNode);
        }

        Errors.Add((_lookahead.Line, _lookahead.Column,
            "Ожидалось '{' или ';' после объявления функции."));
        SkipToSemicolon();
        return new LiteralNode("error", nameToken.Lexeme, nameToken.Line, nameToken.Column);
    }

    private AstNode? ParseCppVarDeclaration()
    {
        var typeTok = Consume();

        SkipCppTemplateArguments();

        if (_lookahead.Type != TokenType.Identifier)
        {
            // нет идентификатора - просто выходим без ошибки
            return null;
        }

        var nameTok = Consume();
        return ParseCppVarDeclTail(typeTok, nameTok);
    }

    // ---------- операторы ----------

    private AstNode? ParseCppStatement()
    {
        if (IsCppType(_lookahead))
            return ParseCppVarDeclaration();

        if (Is(TokenType.Keyword, "if"))
            return ParseCppIfStatement();

        if (Is(TokenType.Keyword, "while"))
            return ParseCppWhileStatement();

        if (Is(TokenType.Keyword, "do"))
            return ParseCppDoWhileStatement();

        if (Is(TokenType.Keyword, "for"))
            return ParseCppForStatement();

        if (Is(TokenType.LBrace))
            return ParseCppBlock();

        if (Is(TokenType.Keyword, "break"))
        {
            Consume();
            if (!Is(TokenType.Semicolon))
                Errors.Add((_lookahead.Line, _lookahead.Column,
                    "Ожидалась ';' после break."));
            else
                Consume();
            return new LiteralNode("keyword", "break", _lookahead.Line, _lookahead.Column);
        }

        if (Is(TokenType.Keyword, "continue"))
        {
            Consume();
            if (!Is(TokenType.Semicolon))
                Errors.Add((_lookahead.Line, _lookahead.Column,
                    "Ожидалась ';' после continue."));
            else
                Consume();
            return new LiteralNode("keyword", "continue", _lookahead.Line, _lookahead.Column);
        }

        if (Is(TokenType.Keyword, "return"))
        {
            Consume();
            var expr = _lookahead.Type == TokenType.Semicolon
                ? new LiteralNode("void", string.Empty, -1, -1)
                : ParseCppExpr();

            if (!Is(TokenType.Semicolon))
                Errors.Add((_lookahead.Line, _lookahead.Column,
                    "Ожидалась ';' после return."));
            else
                Consume();

            return new UnaryNode("return", expr);
        }

        var e = ParseCppExpr();

        if (!Is(TokenType.Semicolon))
        {
            Errors.Add((_lookahead.Line, _lookahead.Column,
                "Ожидалась ';' в конце C++ выражения."));
            if (_lookahead.Type != TokenType.EndOfFile)
                SkipToSemicolon();
        }
        else
        {
            Consume();
        }

        return new ExprStatementNode(e);
    }

    private AstNode ParseCppIfStatement()
    {
        Consume();

        if (!Is(TokenType.LParen))
            Errors.Add((_lookahead.Line, _lookahead.Column, "Ожидалась '(' после if."));
        else
            Consume();

        var condition = ParseCppExpr();

        if (!Is(TokenType.RParen))
            Errors.Add((_lookahead.Line, _lookahead.Column, "Ожидалась ')' в условии if."));
        else
            Consume();

        var thenBranch = ParseCppStatementOrBlock();

        AstNode? elseBranch = null;
        if (Is(TokenType.Keyword, "else"))
        {
            Consume();
            elseBranch = ParseCppStatementOrBlock();
        }

        return new BinaryNode("if", condition,
            new BinaryNode("then-else",
                thenBranch,
                elseBranch ?? new LiteralNode("void", string.Empty, -1, -1)));
    }

    private AstNode ParseCppWhileStatement()
    {
        Consume();

        if (!Is(TokenType.LParen))
            Errors.Add((_lookahead.Line, _lookahead.Column, "Ожидалась '(' после while."));
        else
            Consume();

        var condition = ParseCppExpr();

        if (!Is(TokenType.RParen))
            Errors.Add((_lookahead.Line, _lookahead.Column, "Ожидалась ')' в условии while."));
        else
            Consume();

        var body = ParseCppStatementOrBlock();
        return new BinaryNode("while", condition, body);
    }

    private AstNode ParseCppDoWhileStatement()
    {
        Consume();
        var body = ParseCppStatementOrBlock();

        if (!Is(TokenType.Keyword, "while"))
            Errors.Add((_lookahead.Line, _lookahead.Column,
                "Ожидалась 'while' в конце do-while."));
        else
            Consume();

        if (!Is(TokenType.LParen))
            Errors.Add((_lookahead.Line, _lookahead.Column,
                "Ожидалась '(' после while."));
        else
            Consume();

        var condition = ParseCppExpr();

        if (!Is(TokenType.RParen))
            Errors.Add((_lookahead.Line, _lookahead.Column,
                "Ожидалась ')' в условии do-while."));
        else
            Consume();

        if (!Is(TokenType.Semicolon))
            Errors.Add((_lookahead.Line, _lookahead.Column,
                "Ожидалась ';' в конце do-while."));
        else
            Consume();

        return new BinaryNode("do-while", condition, body);
    }

    private AstNode ParseCppForStatement()
    {
        Consume();

        if (!Is(TokenType.LParen))
            Errors.Add((_lookahead.Line, _lookahead.Column,
                "Ожидалась '(' после for."));
        else
            Consume();

        AstNode init;
        if (Is(TokenType.Semicolon))
        {
            init = new LiteralNode("void", string.Empty, -1, -1);
            Consume();
        }
        else if (IsCppType(_lookahead))
        {
            init = ParseCppVarDeclaration() ??
                   new LiteralNode("void", string.Empty, -1, -1);
        }
        else
        {
            var e = ParseCppExpr();
            if (!Is(TokenType.Semicolon))
                Errors.Add((_lookahead.Line, _lookahead.Column,
                    "Ожидалась ';' после инициализации в for."));
            else
                Consume();
            init = e;
        }

        AstNode cond;
        if (Is(TokenType.Semicolon))
        {
            cond = new LiteralNode("void", string.Empty, -1, -1);
            Consume();
        }
        else
        {
            cond = ParseCppExpr();
            if (!Is(TokenType.Semicolon))
                Errors.Add((_lookahead.Line, _lookahead.Column,
                    "Ожидалась ';' после условия в for."));
            else
                Consume();
        }

        AstNode incr;
        if (Is(TokenType.RParen))
        {
            incr = new LiteralNode("void", string.Empty, -1, -1);
        }
        else
        {
            incr = ParseCppExpr();
        }

        if (!Is(TokenType.RParen))
            Errors.Add((_lookahead.Line, _lookahead.Column,
                "Ожидалась ')' после заголовка for."));
        else
            Consume();

        var body = ParseCppStatementOrBlock();

        return new BinaryNode(
            "for",
            new BinaryNode("for-header",
                init,
                new BinaryNode("for-cond", cond, incr)),
            body);
    }

    private AstNode ParseCppBlock()
    {
        if (!Is(TokenType.LBrace))
            return new LiteralNode("error", string.Empty, _lookahead.Line, _lookahead.Column);

        Consume();
        var stmts = new List<AstNode>();

        while (_lookahead.Type != TokenType.RBrace &&
               _lookahead.Type != TokenType.EndOfFile)
        {
            if (_lookahead.Type == TokenType.Semicolon)
            {
                Consume();
            }
            else
            {
                var stmt = ParseCppStatement();
                if (stmt != null)
                    stmts.Add(stmt);
            }
        }

        if (!Is(TokenType.RBrace))
            Errors.Add((_lookahead.Line, _lookahead.Column, "Ожидалась '}'."));
        else
            Consume();

        return new ProgramNode(stmts);
    }

    private AstNode ParseCppStatementOrBlock()
    {
        if (Is(TokenType.LBrace))
            return ParseCppBlock();

        var stmt = ParseCppStatement();
        return stmt ?? new LiteralNode("void", string.Empty, _lookahead.Line, _lookahead.Column);
    }

    // ---------- выражения ----------

    private AstNode ParseCppExpr() => ParseCppAssignment();

    private AstNode ParseCppAssignment()
    {
        var left = ParseCppLogicalOr();

        if (_lookahead.Type == TokenType.Operator && _lookahead.Lexeme == "=")
        {
            Consume();
            var rhs = ParseCppAssignment();
            return new AssignNode(left, "=", rhs);
        }

        return left;
    }

    private AstNode ParseCppLogicalOr()
    {
        var left = ParseCppLogicalAnd();

        while (_lookahead.Type == TokenType.Operator &&
               _lookahead.Lexeme == "||")
        {
            var op = Consume();
            var right = ParseCppLogicalAnd();
            left = new BinaryNode(op.Lexeme, left, right);
        }

        return left;
    }

    private AstNode ParseCppLogicalAnd()
    {
        var left = ParseCppEquality();

        while (_lookahead.Type == TokenType.Operator &&
               _lookahead.Lexeme == "&&")
        {
            var op = Consume();
            var right = ParseCppEquality();
            left = new BinaryNode(op.Lexeme, left, right);
        }

        return left;
    }

    private AstNode ParseCppEquality()
    {
        var left = ParseCppRelational();

        while (_lookahead.Type == TokenType.Operator &&
               (_lookahead.Lexeme == "==" || _lookahead.Lexeme == "!="))
        {
            var op = Consume();
            var right = ParseCppRelational();
            left = new BinaryNode(op.Lexeme, left, right);
        }

        return left;
    }

    private AstNode ParseCppRelational()
    {
        var left = ParseCppAdditive();

        while (_lookahead.Type == TokenType.Operator &&
               (_lookahead.Lexeme == "<" || _lookahead.Lexeme == ">" ||
                _lookahead.Lexeme == "<=" || _lookahead.Lexeme == ">=" ||
                _lookahead.Lexeme == "<<" || _lookahead.Lexeme == ">>"))
        {
            var op = Consume();
            var right = ParseCppAdditive();
            left = new BinaryNode(op.Lexeme, left, right);
        }

        return left;
    }

    private AstNode ParseCppAdditive()
    {
        var left = ParseCppMultiplicative();

        while (_lookahead.Type == TokenType.Operator &&
               (_lookahead.Lexeme == "+" || _lookahead.Lexeme == "-"))
        {
            var op = Consume();
            var right = ParseCppMultiplicative();
            left = new BinaryNode(op.Lexeme, left, right);
        }

        return left;
    }

    private AstNode ParseCppMultiplicative()
    {
        var left = ParseCppUnary();

        while (_lookahead.Type == TokenType.Operator &&
               (_lookahead.Lexeme == "*" || _lookahead.Lexeme == "/" ||
                _lookahead.Lexeme == "%"))
        {
            var op = Consume();
            var right = ParseCppUnary();
            left = new BinaryNode(op.Lexeme, left, right);
        }

        return left;
    }

    private AstNode ParseCppUnary()
    {
        if (_lookahead.Type == TokenType.Operator &&
            (_lookahead.Lexeme is "!" or "-" or "+" or "++" or "--" or "~" or "&" or "*"))
        {
            var op = Consume();
            var operand = ParseCppUnary();
            return new UnaryNode(op.Lexeme, operand);
        }

        return ParseCppPostfix();
    }

    private AstNode ParseCppPostfix()
    {
        var expr = ParseCppPrimary();

        while (true)
        {
            if (_lookahead.Type == TokenType.Operator &&
                (_lookahead.Lexeme == "++" || _lookahead.Lexeme == "--"))
            {
                var op = Consume();
                expr = new UnaryNode(op.Lexeme + "_post", expr);
            }
            else if (_lookahead.Type == TokenType.LBracket)
            {
                Consume();
                var index = ParseCppExpr();
                if (!Is(TokenType.RBracket))
                    Errors.Add((_lookahead.Line, _lookahead.Column, "Ожидалась ']'."));
                else
                    Consume();
                expr = new BinaryNode("[]", expr, index);
            }
            else if (_lookahead.Type == TokenType.Dot)
            {
                Consume();
                if (_lookahead.Type != TokenType.Identifier)
                {
                    Errors.Add((_lookahead.Line, _lookahead.Column,
                        "Ожидался идентификатор после '.'."));
                    break;
                }
                var idTok = Consume();
                var member = new IdentifierNode(idTok.Lexeme, idTok.Line, idTok.Column);
                expr = new BinaryNode(".", expr, member);
            }
            else if (_lookahead.Type == TokenType.Arrow)
            {
                Consume();
                if (_lookahead.Type != TokenType.Identifier)
                {
                    Errors.Add((_lookahead.Line, _lookahead.Column,
                        "Ожидался идентификатор после '->'."));
                    break;
                }
                var idTok = Consume();
                var member = new IdentifierNode(idTok.Lexeme, idTok.Line, idTok.Column);
                expr = new BinaryNode("->", expr, member);
            }
            else if (_lookahead.Type == TokenType.LParen)
            {
                Consume();
                var args = new List<AstNode>();
                while (!Is(TokenType.RParen) &&
                       _lookahead.Type != TokenType.EndOfFile)
                {
                    args.Add(ParseCppExpr());
                    if (Is(TokenType.Comma))
                        Consume();
                    else if (!Is(TokenType.RParen))
                        break;
                }

                if (!Is(TokenType.RParen))
                    Errors.Add((_lookahead.Line, _lookahead.Column,
                        "Ожидалась ')' в списке аргументов."));
                else
                    Consume();

                expr = new BinaryNode("call", expr, new ProgramNode(args));
            }
            else
            {
                break;
            }
        }

        return expr;
    }

    private AstNode ParseCppPrimary()
    {
        if (_lookahead.Type == TokenType.Identifier)
        {
            var t = Consume();
            return new IdentifierNode(t.Lexeme, t.Line, t.Column);
        }

        if (_lookahead.Type == TokenType.Number)
        {
            var t = Consume();
            return new LiteralNode("number", t.Lexeme, t.Line, t.Column);
        }

        if (_lookahead.Type == TokenType.StringLiteral)
        {
            var t = Consume();
            return new LiteralNode("string", t.Lexeme, t.Line, t.Column);
        }

        if (_lookahead.Type == TokenType.CharLiteral)
        {
            var t = Consume();
            return new LiteralNode("char", t.Lexeme, t.Line, t.Column);
        }

        if (Is(TokenType.Keyword, "true") || Is(TokenType.Keyword, "false"))
        {
            var t = Consume();
            return new LiteralNode("bool", t.Lexeme, t.Line, t.Column);
        }

        if (_lookahead.Type == TokenType.LParen)
        {
            Consume();
            var expr = ParseCppExpr();
            if (!Is(TokenType.RParen))
                Errors.Add((_lookahead.Line, _lookahead.Column,
                    "Ожидалась ')' в выражении."));
            else
                Consume();
            return expr;
        }

        if (_lookahead.Type == TokenType.Semicolon ||
            _lookahead.Type == TokenType.Operator ||
            _lookahead.Type == TokenType.EndOfFile)
        {
            Errors.Add((_lookahead.Line, _lookahead.Column,
                $"Ожидалось выражение, получено '{_lookahead.Lexeme}'"));
            return new LiteralNode("error", "?", _lookahead.Line, _lookahead.Column);
        }

        var err = Consume();
        Errors.Add((err.Line, err.Column,
            $"Неожиданный токен: '{err.Lexeme}'"));
        return new LiteralNode("error", err.Lexeme, err.Line, err.Column);
    }

    private void SkipToSemicolon()
    {
        int depth = 0;

        while (_lookahead.Type != TokenType.EndOfFile)
        {
            if (_lookahead.Type is TokenType.LParen or TokenType.LBrace or TokenType.LBracket)
                depth++;
            else if (_lookahead.Type is TokenType.RParen or TokenType.RBrace or TokenType.RBracket)
                depth--;

            if (depth == 0 && _lookahead.Type == TokenType.Semicolon)
            {
                Consume();
                break;
            }

            MoveNext();
        }
    }
}
