// File: Program.cs
using Lexer;
using Parser;
using System;
using System.IO;

class Program
{
    public static void Run(string source, LanguageProfile profile, string title)
    {
        Console.WriteLine($"=== {title} [{profile}] ===");

        // 1. Лексический анализ
        var lex = new LexAnalyzer(source, profile);
        var tokens = lex.Scan();

        // 2. Синтаксический анализ
        var parser = new SyntaxAnalyzer(tokens, profile);
        var ast = parser.ParseProgram();

        // 3. Вывод дерева разбора
        if (ast != null)
            AstPrinter.PrintDeepTree(ast);

        // 4. Диагностика ошибок
        if (lex.Errors.Count > 0 || parser.Errors.Count > 0)
        {
            Console.WriteLine("\n=== ОШИБКИ ===");
            Console.ForegroundColor = ConsoleColor.Red;
            foreach (var (line, col, msg) in lex.Errors)
                Console.WriteLine($"Лексическая ошибка в {line}:{col} - {msg}");
            foreach (var (line, col, msg) in parser.Errors)
                Console.WriteLine($"Синтаксическая ошибка в {line}:{col} - {msg}");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nСинтаксический анализ: OK");
            Console.ResetColor();
        }
        Console.WriteLine();
    }

    static void Main(string[] args)
    {
        // Примеры из командной строки или из кода
        if (args.Length > 0)
        {
            // Чтение из файла
            string filePath = args[0];
            LanguageProfile profile = args.Length > 1 && args[1].ToLower() == "cpp"
                ? LanguageProfile.Cpp
                : LanguageProfile.Variant14;

            if (!File.Exists(filePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"ОШИБКА: Файл '{filePath}' не найден.");
                Console.ResetColor();
                return;
            }

            string source = File.ReadAllText(filePath);
            Run(source, profile, $"Файл: {filePath}");
        }
        else
        {
            // Встроенные примеры для демонстрации
            Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  СИНТАКСИЧЕСКИЙ АНАЛИЗАТОР - Лабораторная работа №5        ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

            // Примеры Variant14
            string v14_ok = "x := 'T' or y and not 'F'; z := a xor b;";
            string v14_err = "x := 'T' or y; z := ; w := 'T';"; // ошибка: "z := ;"

            Run(v14_ok, LanguageProfile.Variant14, "Variant14 корректный");
            Run(v14_err, LanguageProfile.Variant14, "Variant14 с ошибкой");

            // Примеры C++
            string cpp_ok = "int x = 1 + 2 * 3; bool f = x > 0 && x < 10;";
            string cpp_declarations = "int x = 5; float y = 3.14f; bool flag = true;";
            string cpp_complex = "int result = (a + b) * (c - d) / 2; bool test = x >= 0 && y <= 100;";
            string cpp_err = "int x = 1 + ; bool f = && x;"; // синтаксические ошибки

            Run(cpp_ok, LanguageProfile.Cpp, "C++ простой");
            Run(cpp_declarations, LanguageProfile.Cpp, "C++ объявления");
            Run(cpp_complex, LanguageProfile.Cpp, "C++ сложные выражения");
            Run(cpp_err, LanguageProfile.Cpp, "C++ с ошибками");

            Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  Для анализа файла используйте:                            ║");
            Console.WriteLine("║  > Program.exe <файл> [cpp|variant14]                      ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        }
    }
}
