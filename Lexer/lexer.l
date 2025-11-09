%option noyywrap yylineno

%{
    #include <stdio.h>
    int ch; /* column */
%}

/* Macros */
digit      [0-9]
letter     [a-zA-Z_]
ident      {letter}[a-zA-Z0-9_]*
ws         [ \t\r\n]+
delim      [();]
assign     ":="
kw         or|xor|and|not
tfchar     \'[TF]\'

%%

{kw}        { printf("KEYWORD (%d,%d): %s\n", yylineno, ch, yytext); ch += yyleng; }
{assign}    { printf("ASSIGN  (%d,%d): %s\n", yylineno, ch, yytext); ch += yyleng; }
{delim}     { printf("DELIM   (%d,%d): %s\n", yylineno, ch, yytext); ch += yyleng; }
{tfchar}    { printf("CHAR    (%d,%d): %s\n", yylineno, ch, yytext); ch += yyleng; }
{ident}     { printf("IDENT   (%d,%d): %s\n", yylineno, ch, yytext); ch += yyleng; }
{ws}        { ch += yyleng; /* skip */ }
.           { printf("UNKNOWN (%d,%d): %s\n", yylineno, ch, yytext); ch += yyleng; }

%%

int main(int argc, char** argv)
{
    if (argc < 2) { printf("Нужен файл.\n"); return -1; }
    yyin = fopen(argv[1], "r");
    if (!yyin) { printf("Не смог открыть файл %s.\n", argv[1]); return -1; }
    ch = 1;
    yylineno = 1;
    yylex();
    fclose(yyin);
    return 0;
}
