/* File: parser.y */

%{
    #include <stdio.h>
    #include <stdlib.h>
    #include <string.h>

    int yylex(void);
    void yyerror(const char *s);

    typedef enum {
        NODE_PROGRAM,
        NODE_ASSIGN,
        NODE_OR,
        NODE_XOR,
        NODE_AND,
        NODE_NOT,
        NODE_IDENT,
        NODE_CHAR
    } NodeKind;

    typedef struct Node {
        NodeKind kind;
        char *text;             /* для идентификаторов и литералов */
        struct Node *left;      /* левый потомок (или первый) */
        struct Node *right;     /* правый потомок (для бинарных) */
        struct Node *next;      /* для списка операторов program */
    } Node;

    static Node *root_ast = NULL;

    /* фабрики узлов */

    Node *make_node(NodeKind kind, Node *left, Node *right, const char *text)
    {
        Node *n = (Node*)malloc(sizeof(Node));
        if (!n) {
            fprintf(stderr, "Out of memory\n");
            exit(1);
        }
        n->kind = kind;
        n->left = left;
        n->right = right;
        n->next = NULL;
        if (text) {
            n->text = (char*)malloc(strlen(text) + 1);
            if (!n->text) {
                fprintf(stderr, "Out of memory\n");
                exit(1);
            }
            strcpy(n->text, text);
        } else {
            n->text = NULL;
        }
        return n;
    }

    Node *make_ident(const char *name)
    {
        return make_node(NODE_IDENT, NULL, NULL, name);
    }

    Node *make_char(const char *lexeme)
    {
        /* сюда приходит yytext вида "'T'" или "'F'" */
        return make_node(NODE_CHAR, NULL, NULL, lexeme);
    }

    Node *make_unary(NodeKind kind, Node *operand)
    {
        return make_node(kind, operand, NULL, NULL);
    }

    Node *make_binary(NodeKind kind, Node *left, Node *right)
    {
        return make_node(kind, left, right, NULL);
    }

    void print_ast(Node *node, const char *prefix, int is_last)
    {
        if (!node) return;

        printf("%s%s", prefix, is_last ? "└─ " : "├─ ");

        switch (node->kind)
        {
            case NODE_PROGRAM:
                printf("Program\n");
                break;
            case NODE_ASSIGN:
                printf("Assign\n");
                break;
            case NODE_OR:
                printf("Or\n");
                break;
            case NODE_XOR:
                printf("Xor\n");
                break;
            case NODE_AND:
                printf("And\n");
                break;
            case NODE_NOT:
                printf("Not\n");
                break;
            case NODE_IDENT:
                printf("Id(%s)\n", node->text ? node->text : "?");
                break;
            case NODE_CHAR:
                printf("Char(%s)\n", node->text ? node->text : "?");
                break;
            default:
                printf("Unknown\n");
                break;
        }

        Node *children[3];
        int count = 0;

        if (node->left)  children[count++] = node->left;
        if (node->right) children[count++] = node->right;
        if (node->next)  children[count++] = node->next;

        char new_prefix[256];
        snprintf(new_prefix, sizeof(new_prefix), "%s%s",
                 prefix, is_last ? "   " : "│  ");

        for (int i = 0; i < count; ++i)
        {
            int last_child = (i == count - 1);
            print_ast(children[i], new_prefix, last_child);
        }
    }
%}

/* значения всех нетерминалов/нужных токенов — указатель на Node */
%union {
    struct Node *node;
}

/* токены, которые несут готовые узлы */
%token <node> IDENT CHAR_T CHAR_F

%token KW_OR KW_XOR KW_AND KW_NOT
%token ASSIGN
%token LPAREN RPAREN SEMICOLON

%type <node> program statement_list statement expr term factor primary

%%

program
    : statement_list
        {
            $$ = make_node(NODE_PROGRAM, $1, NULL, NULL);
            root_ast = $$;
        }
    ;

statement_list
    : statement
        {
            $$ = $1;
        }
    | statement_list statement
        {
            Node *p = $1;
            while (p->next) p = p->next;
            p->next = $2;
            $$ = $1;
        }
    ;

statement
    : IDENT ASSIGN expr SEMICOLON
        {
            $$ = make_binary(NODE_ASSIGN, $1, $3);
        }
    ;

/* expr: or/xor уровень */
expr
    : term
        { $$ = $1; }
    | expr KW_OR  term
        { $$ = make_binary(NODE_OR,  $1, $3); }
    | expr KW_XOR term
        { $$ = make_binary(NODE_XOR, $1, $3); }
    ;

/* term: and уровень */
term
    : factor
        { $$ = $1; }
    | term KW_AND factor
        { $$ = make_binary(NODE_AND, $1, $3); }
    ;

/* factor: not уровень */
factor
    : KW_NOT factor
        { $$ = make_unary(NODE_NOT, $2); }
    | primary
        { $$ = $1; }
    ;

/* primary: листья */
primary
    : CHAR_T
        { $$ = $1; }      /* уже Char('T') из лексера */
    | CHAR_F
        { $$ = $1; }      /* уже Char('F') из лексера */
    | IDENT
        { $$ = $1; }      /* Id(...) из лексера */
    | LPAREN expr RPAREN
        { $$ = $2; }      /* скобки не создают отдельный узел */
    ;

%%

void yyerror(const char *s)
{
    fprintf(stderr, "Syntax error: %s\n", s);
}

int main(int argc, char **argv)
{
    extern FILE *yyin;

    if (argc < 2)
    {
        fprintf(stderr, "I NEED FILE.\n");
        return 1;
    }

    yyin = fopen(argv[1], "r");
    if (!yyin)
    {
        fprintf(stderr, "CAN'T OPEN FILE %s.\n", argv[1]);
        return 1;
    }

    if (yyparse() == 0)
    {
        printf("=== AST ===\n");
        print_ast(root_ast, "", 1);
        printf("OK\n");
    }
    else
    {
        printf("NOT OK\n");
    }

    fclose(yyin);
    return 0;
}
