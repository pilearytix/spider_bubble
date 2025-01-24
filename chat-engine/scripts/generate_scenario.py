from jinja2 import Template, Environment, FileSystemLoader
import yaml
import json
import os

def load_template(template_path):
    # Get the template directory and filename
    template_dir = os.path.dirname(template_path)
    template_name = os.path.basename(template_path)
    
    # Create Jinja2 environment with the template directory
    env = Environment(loader=FileSystemLoader(template_dir))
    
    # Add the tojson filter if not already present
    if 'tojson' not in env.filters:
        env.filters['tojson'] = json.dumps
    
    # Load and return the template
    return env.get_template(template_name)

def load_config(config_path):
    with open(config_path, 'r') as file:
        return yaml.safe_load(file)

def validate_config(config):
    required_keys = ['objects', 'question_structure', 'responses', 'actions']
    for key in required_keys:
        if key not in config:
            raise ValueError(f"Missing required key: {key}")
    
    # Validate responses structure
    responses = config['responses']
    if not all(key in responses for key in ['acknowledgements', 'outcomes', 'speculations']):
        raise ValueError("Responses must contain acknowledgements, outcomes, and speculations")
    
    # Validate actions structure
    actions = config['actions']
    if not all(key in actions for key in ['objects', 'npcs', 'locations']):
        raise ValueError("Actions must contain objects, npcs, and locations sections")

def generate_scenario(template_path, config_path, output_path):
    template = load_template(template_path)
    config = load_config(config_path)
    
    # Validate the configuration
    validate_config(config)
    
    # Render the template with the configuration
    result = template.render(
        objects=config['objects'],
        question_structure=config['question_structure'],
        responses=config['responses'],
        actions=config['actions']
    )
    
    try:
        # Validate the generated JSON
        json_result = json.loads(result)
    except json.JSONDecodeError as e:
        raise ValueError(f"Generated invalid JSON: {e}")
    
    # Create output directory if it doesn't exist
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    
    # Save the generated JSON with proper formatting
    with open(output_path, 'w') as file:
        json.dump(json_result, file, indent=4)

if __name__ == "__main__":
    import argparse
    
    parser = argparse.ArgumentParser(description='Generate a scenario JSON file')
    parser.add_argument('--template', required=True, help='Path to the Jinja2 template file')
    parser.add_argument('--config', required=True, help='Path to the YAML configuration file')
    parser.add_argument('--output', required=True, help='Path for the output JSON file')
    
    args = parser.parse_args()
    
    try:
        generate_scenario(args.template, args.config, args.output)
        print(f"Successfully generated scenario at {args.output}")
    except Exception as e:
        print(f"Error generating scenario: {e}")
        exit(1) 