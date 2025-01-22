#!/bin/bash

echo "Setting up Text Combinations Generator environment..."

# Check if Python is installed
if ! command -v python3 &> /dev/null; then
    echo "Python 3 is required but not installed. Please install Python 3 and try again."
    exit 1
fi

# Create virtual environment if it doesn't exist
if [ ! -d "venv" ]; then
    echo "Creating virtual environment..."
    python3 -m venv venv
else
    echo "Virtual environment already exists."
fi

# Activate virtual environment
echo "Activating virtual environment..."
source venv/bin/activate

# Install dependencies
echo "Installing dependencies..."
pip install -r requirements.txt

echo "Setup complete! You can now run the generators:"
echo "1. For interactive generation: python text-combinations/generate-interactive.py"
echo "2. For random generation: python text-combinations/generate-random.py" 
echo "3. For scenario generation: python scripts/generate_scenario.py --template templates/example_config.yaml --config configs/example_config.yaml --output scenarios/example_scenario.json"
