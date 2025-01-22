@echo off
echo Setting up Text Combinations Generator environment...

REM Check if Python is installed
python --version >nul 2>&1
if errorlevel 1 (
    echo Python is required but not installed. Please install Python and try again.
    exit /b 1
)

REM Create virtual environment if it doesn't exist
if not exist venv (
    echo Creating virtual environment...
    python -m venv venv
) else (
    echo Virtual environment already exists.
)

REM Activate virtual environment
echo Activating virtual environment...
call venv\Scripts\activate.bat

REM Install dependencies
echo Installing dependencies...
pip install -r requirements.txt

echo Setup complete! You can now run the generators:
echo 1. For interactive generation: python text-combinations/generate-interactive.py
echo 2. For random generation: python text-combinations/generate-random.py 