using System;
using System.Collections.Generic;

namespace Parser;

// Базовый узел
public abstract record AstNode;

// Корень - набор операторов/выражений
public record ProgramNode(IReadOnlyList<AstNode> Children) : AstNode;

// Оператор вида "expr;" (для Cpp)
public record ExprStatementNode(AstNode Expr) : AstNode;

// Присваивание: left op right (":=" для Variant14, "=" для C++)
public record AssignNode(AstNode Left, string Op, AstNode Right) : AstNode;

// Бинарный оператор: +, -, *, /, &&, ||, or, and, xor, ==, < и т.п.
public record BinaryNode(string Op, AstNode Left, AstNode Right) : AstNode;

// Унарный оператор: not, !, - и т.п.
public record UnaryNode(string Op, AstNode Operand) : AstNode;

// Идентификатор
public record IdentifierNode(string Name, int Line, int Column) : AstNode;

// Литерал (число, строка, char, 'T'/'F' как char)
public record LiteralNode(string Kind, string Value, int Line, int Column) : AstNode;


public static class AstPrinter
{
    public static void PrintDeepTree(AstNode root)
    {
        if (root == null)
        {
            Console.WriteLine("<empty>");
            return;
        }

        int maxDepth = GetDepth(root); // глубина в узлах: root = 1
        Console.WriteLine("Дерево разбора:");
        PrintNode(root, prefix: string.Empty, isLast: true, depth: 1, maxDepth);
    }

    // ===== вычисление глубины =====
    private static int GetDepth(AstNode node)
    {
        if (node == null) return 0;

        var children = GetChildren(node);
        if (children.Count == 0) return 1;

        int max = 0;
        foreach (var ch in children)
            max = Math.Max(max, GetDepth(ch));

        return 1 + max;
    }

    // ===== печать узла =====
    private static void PrintNode(
        AstNode node,
        string prefix,
        bool isLast,
        int depth,
        int maxDepth)
    {
        var children = GetChildren(node);

        // Лист: рисуем длинный хвост из '─' до максимальной глубины
        if (children.Count == 0)
        {
            int extraLevels = maxDepth - depth;          // сколько уровней ниже
            int dashCount = 2 + extraLevels * 4;       // длина хвоста (подбери под вкус)

            Console.Write(prefix);
            Console.Write(isLast ? "└" : "├");
            Console.Write(new string('─', dashCount));
            Console.Write(" ");
            Console.WriteLine(NodeLabel(node));
            return;
        }

        // Внутренний узел — обычный "└──"/"├──", без хвоста
        Console.Write(prefix);
        Console.Write(isLast ? "└── " : "├── ");
        Console.WriteLine(NodeLabel(node));

        // Префикс для детей: либо вертикальная линия, либо пробел
        string childPrefix = prefix + (isLast ? "    " : "│   ");

        for (int i = 0; i < children.Count; i++)
        {
            bool childIsLast = (i == children.Count - 1);
            PrintNode(children[i], childPrefix, childIsLast, depth + 1, maxDepth);
        }
    }

    // ===== дети для обхода (до двух «основных») =====
    private static List<AstNode> GetChildren(AstNode node)
    {
        var list = new List<AstNode>();

        switch (node)
        {
            case ProgramNode p:
                list.AddRange(p.Children);           // Program: все операторы
                break;

            case ExprStatementNode es:
                list.Add(es.Expr);
                break;

            case AssignNode a:
                list.Add(a.Left);
                list.Add(a.Right);
                break;

            case BinaryNode b:
                list.Add(b.Left);
                list.Add(b.Right);
                break;

            case UnaryNode u:
                list.Add(u.Operand);
                break;

                // IdentifierNode / LiteralNode — лист
        }

        return list;
    }

    // ===== человекочитаемые подписи =====
    private static string NodeLabel(AstNode node)
    {
        return node switch
        {
            ProgramNode => "Program",
            ExprStatementNode es => "ExprStmt",
            AssignNode a => $"Assign({a.Op})",
            BinaryNode b => $"Bin({b.Op})",
            UnaryNode u => $"Un({u.Op})",
            IdentifierNode id => $"Id({id.Name})",
            LiteralNode lit => $"{lit.Kind}({lit.Value})",
            _ => node.GetType().Name
        };
    }
}
