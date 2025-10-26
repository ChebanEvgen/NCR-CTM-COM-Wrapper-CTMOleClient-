@echo off
setlocal

REM 1. Получаем дату и время в формате YYYYMMDDhhmmss
REM Этот метод минимизирует риски, убирая все символы-разделители
for /f "delims=" %%a in ('wmic OS Get localdatetime /value') do (
    set "dt_wmic=%%a"
)

REM Извлекаем компоненты для имени файла
set "DATE_TIME=%dt_wmic:~4,8%_%dt_wmic:~12,6%"
set "RANDOM_ID=%RANDOM%"
set "FILENAME=diff_report_%DATE_TIME%_%RANDOM_ID%.txt"

echo ---------------------------------------------------- 
echo Отчет о разнице (diff) проекта >> %FILENAME%
echo Дата/время: %DATE_TIME% >> %FILENAME%
echo Сравнение: HEAD (последний коммит) vs Рабочая директория >> %FILENAME%
echo ---------------------------------------------------- >> %FILENAME%

REM 2. Выполняем git diff. Используем кавычки для имени файла,
REM чтобы гарантировать правильную передачу аргументов.
REM Сравниваем последний коммит (HEAD) с рабочим каталогом.
git diff HEAD > "%FILENAME%" 2>&1

echo.
echo ----------------------------------------------------
echo Разница успешно сохранена в файл: %FILENAME%
echo ----------------------------------------------------

endlocal
pause