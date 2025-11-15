@echo off
REM Скрипт сборки и запуска парсера (Bison + Flex + GCC)

setlocal enabledelayedexpansion

REM Папка со скриптом = папка с parser.y и lexer.l
set SRC_DIR=%~dp0

echo === Bison: генерация parser.tab.c / parser.tab.h / parser.output ===
bison -d -v "%SRC_DIR%parser.y"
if errorlevel 1 (
    echo [ERROR] Bison завершился с ошибкой.
    exit /b 1
)

echo === Flex: генерация lex.yy.c ===
flex "%SRC_DIR%lexer.l"
if errorlevel 1 (
    echo [ERROR] Flex завершился с ошибкой.
    exit /b 1
)

echo === GCC: компоновка parser.exe ===
REM Если используешь MSYS2 / MinGW, gcc должен быть в PATH.
REM В типичной связке Flex+Bison нужна библиотека -lfl.
gcc -o "%SRC_DIR%parser.exe" "%SRC_DIR%parser.tab.c" "%SRC_DIR%lex.yy.c" -lfl
if errorlevel 1 (
    echo [ERROR] GCC завершился с ошибкой.
    exit /b 1
)

REM Определяем входной файл: параметр bat или test.txt по умолчанию
if "%~1"=="" (
    set INPUT=test.txt
) else (
    set INPUT=%~1
)

echo === Запуск parser.exe на файле "%INPUT%" ===
"%SRC_DIR%parser.exe" "%INPUT%"

endlocal
