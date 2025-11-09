// File: Program.cs
using Lexer;
using System;
using System.IO;

class Program
{
    static void DumpTokens(string title, string text, LanguageProfile profile)
    {
        Console.WriteLine($"--- {title} [{profile}] ---");
        var lex = new LexAnalyzer(text, profile);

        foreach (var tok in lex.Scan())
        {
            if (tok.Type == TokenType.Whitespace || tok.Type == TokenType.Comment) continue;
            Console.WriteLine($"{tok.Line}:{tok.Column}\t{tok.Type,-14}\t{tok.Lexeme}");
            if (tok.Type == TokenType.EndOfFile) break;
        }

        // Выводим все лексические ошибки после завершения сканирования
        if (lex.Errors.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n=== Лексические ошибки ({lex.Errors.Count}) ===");
            foreach (var (line, col, msg) in lex.Errors)
            {
                Console.Error.WriteLine($"Лексическая ошибка в {line}:{col} - {msg}");
            }
            Console.ResetColor();
        }

        Console.WriteLine();
    }

    static void DumpTokensFromFile(string filePath, LanguageProfile profile)
    {
        if (!File.Exists(filePath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"ОШИБКА: Файл '{filePath}' не найден.");
            Console.ResetColor();
            return;
        }
        string text;
        try
        {
            text = File.ReadAllText(filePath);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Ошибка: Файл не удалось прочитать'{filePath}': {ex.Message}");
            Console.ResetColor();
            return;
        }
        var fileInfo = new FileInfo(filePath);
        Console.WriteLine($"--- Файл: {filePath} [{profile}] ---");
        Console.WriteLine();
        var lex = new LexAnalyzer(text, profile);
        int tokenCount = 0;
        foreach (var tok in lex.Scan())
        {
            if (tok.Type == TokenType.Whitespace || tok.Type == TokenType.Comment)
                continue;
            Console.WriteLine($"{tok.Line}:{tok.Column}\t{tok.Type,-14}\t{tok.Lexeme}");
            tokenCount++;
            if (tok.Type == TokenType.EndOfFile)
                break;
        }

        Console.WriteLine();

        // Выводим статистику
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"=== СТАТИСТИКА ===");
        Console.WriteLine($"Всего значащих токенов: {tokenCount}");
        Console.ResetColor();

        // Выводим ошибки, если они есть
        if (lex.Errors.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n=== Лексические ошибки ({lex.Errors.Count}) ===");
            foreach (var (line, col, msg) in lex.Errors)
            {
                Console.Error.WriteLine($"Лексическая ошибка в {line}:{col} - {msg}");
            }
            Console.ResetColor();
        }

        Console.WriteLine();
    }

    static void Main(string[] args)
    {
        // 1) Вариант 14: вход из строки
        string s1 = "x := 'T' or y and not 'F'; :;z := a xor b;";
        DumpTokens("Variant14 / string", s1, LanguageProfile.Variant14);

        // 2) Расширенный режим C++: строки, числа, операторы, препроцессор
        string cpp = @"#include <stdio.h>
    // line comment
    int main(){ int xyz=12.2.3.3.3.3, y=aa; k = i+++++j; printf(""%d\n"", x+y; return 0; }";
        DumpTokens("Cpp / string", cpp, LanguageProfile.Cpp);
        DumpTokensFromFile("test.cpp", LanguageProfile.Cpp);



    }

}
