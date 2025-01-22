# Text Combinations Generator

A Python-based text generation tool that creates interactive conversations using predefined templates and random combinations.

## Quick Setup (Recommended)

### Using Automated Setup Scripts

1. On macOS/Linux:
```bash
./setup.sh
```

2. On Windows:
```bash
setup.bat
```

The scripts will create a virtual environment, activate it, and install all dependencies automatically.

## Manual Setup

If you prefer to set up manually:

1. Create a virtual environment:
```bash
python -m venv venv
```

2. Activate the virtual environment:
- On macOS/Linux:
```bash
source venv/bin/activate
```
- On Windows:
```bash
.\venv\Scripts\activate
```

3. Install dependencies:
```bash
pip install -r requirements.txt
```

## Using the Virtual Environment

1. Before running any Python scripts, make sure the virtual environment is activated:
   - You'll see `(venv)` at the start of your terminal prompt when it's active
   - If not active, activate it using the commands in the Setup section above
   - You need to activate the venv each time you open a new terminal

2. Once activated, you can run the scripts:
```bash
sudo python text-combinations/generate-interactive.py
# or
sudo python text-combinations/generate-random.py
```

3. To deactivate the virtual environment when you're done:
```bash
deactivate
```

## Available Scripts

1. Interactive Generator (`generate-interactive.py`):
   - Use UP ARROW to regenerate current line
   - Press ENTER to proceed to next line
   - Press ESC to exit

2. Random Generator (`generate-random.py`):
   - Generates a complete conversation in one go

## Data Structure

The conversations are generated using templates and options defined in `data.json`. The structure includes:
- Questions with curious observations
- Initial responses with acknowledgements
- Follow-up responses with specific actions and targets
