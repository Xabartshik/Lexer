%option noyywrap yylineno

%{
  #include "parser.tab.h"

  int ch; /* column */

  /* Фабрики из parser.y */
  struct Node *make_ident(const char *name);
  struct Node *make_char(const char *lexeme);
%}

/* Macros */

digit  [0-9]
letter [a-zA-Z_]
ident  {letter}[a-zA-Z0-9_]*
ws     [ \t\r\n]+

%%

"or"      { ch += yyleng; return KW_OR;  }
"xor"     { ch += yyleng; return KW_XOR; }
"and"     { ch += yyleng; return KW_AND; }
"not"     { ch += yyleng; return KW_NOT; }
":="      { ch += yyleng; return ASSIGN; }
"("       { ch += yyleng; return LPAREN; }
")"       { ch += yyleng; return RPAREN; }
";"       { ch += yyleng; return SEMICOLON; }
"'T'"     { ch += yyleng; yylval.node = make_char(yytext); return CHAR_T; }
"'F'"     { ch += yyleng; yylval.node = make_char(yytext); return CHAR_F; }
{ident}   { ch += yyleng; yylval.node = make_ident(yytext); return IDENT; }
{ws}      { ch += yyleng; /* skip */ }

.         {
            fprintf(stderr, "UNKNOWN (%d,%d): %s\n", yylineno, ch, yytext);
            ch += yyleng;
          }

%%
