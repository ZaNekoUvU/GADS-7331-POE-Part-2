@echo off
setlocal
cd /d "%~dp0"

where py >nul 2>nul
if %errorlevel%==0 (
    py -3 "deck_builder_visual.py"
) else (
    python "deck_builder_visual.py"
)

echo.
echo Game closed. Press any key to exit.
pause >nul
