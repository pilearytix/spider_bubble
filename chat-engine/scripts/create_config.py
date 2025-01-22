import yaml
from typing import Dict, List
import os

def get_multiple_inputs(prompt: str, min_items: int = 2) -> List[str]:
    """Helper function to collect multiple string inputs"""
    items = []
    print(f"\n{prompt}")
    print(f"(Enter at least {min_items} items. Press Enter twice to finish)")
    
    while True:
        item = input("> ").strip()
        if not item and len(items) >= min_items:
            break
        elif not item:
            print(f"Please enter at least {min_items} items.")
            continue
        items.append(item)
    return items

def collect_object_details() -> Dict:
    """Collect object information including observations, causes, and mystical questions"""
    objects = {}
    
    while True:
        object_name = input("\nEnter an object name (or press Enter to finish): ").strip()
        if not object_name:
            if objects:
                break
            print("Please enter at least one object.")
            continue
            
        object_data = {
            "observations": get_multiple_inputs("Enter observations for this object (e.g., 'is the water pressure lower than usual?'):"),
            "causes": get_multiple_inputs("Enter possible causes (e.g., 'of those old pipes finally giving up?'):"),
            "mystical_questions": get_multiple_inputs("Enter mystical questions (e.g., 'Should we call maintenance right away?'):")
        }
        objects[object_name] = object_data
    
    return objects

def collect_responses() -> Dict:
    """Collect response information including acknowledgements and speculations"""
    responses = {
        "acknowledgements": get_multiple_inputs("Enter acknowledgement phrases:"),
        "speculations": get_multiple_inputs("Enter speculation phrases:"),
        "outcomes": {}
    }
    
    # Collect outcomes for each object
    print("\nNow let's set up outcomes for each object:")
    while True:
        object_name = input("\nEnter an object name for outcomes (or press Enter to finish): ").strip()
        if not object_name:
            if responses["outcomes"]:
                break
            print("Please enter at least one object for outcomes.")
            continue
            
        outcomes = {
            "left": get_multiple_inputs("Enter left-side outcomes:"),
            "left_items": get_multiple_inputs("Enter left-side items:"),
            "right": get_multiple_inputs("Enter right-side outcomes:"),
            "right_items": get_multiple_inputs("Enter right-side items:")
        }
        responses["outcomes"][object_name] = outcomes
    
    return responses

def collect_actions() -> Dict:
    """Collect action information for objects, NPCs, and locations"""
    actions = {
        "objects": {},
        "npcs": {},
        "locations": {}
    }
    
    for category in ["objects", "npcs", "locations"]:
        print(f"\nLet's add {category}:")
        while True:
            item_name = input(f"\nEnter a {category[:-1]} name (or press Enter to finish): ").strip()
            if not item_name:
                break
                
            item_data = {
                "name": input(f"Enter the display name for {item_name}: "),
                "actions": get_multiple_inputs("Enter possible actions:"),
                "valid_results": get_multiple_inputs("Enter valid results for these actions:")
            }
            actions[category][item_name] = item_data
    
    return actions

def main():
    print("Welcome to the YAML Configuration Generator!")
    print("This script will help you create a configuration file similar to the example.")
    
    config = {}
    
    # Collect objects
    print("\nFirst, let's define your objects and their properties:")
    config["objects"] = collect_object_details()
    
    # Set question structure
    config["question_structure"] = input("\nEnter the question structure template: ")
    
    # Collect responses
    print("\nNow, let's set up the responses section:")
    config["responses"] = collect_responses()
    
    # Collect actions
    print("\nFinally, let's define the actions:")
    config["actions"] = collect_actions()
    
    # Save to file
    filename = input("\nEnter the filename to save (e.g., 'config.yaml'): ")
    with open(filename, 'w', encoding='utf-8') as f:
        yaml.dump(config, f, sort_keys=False, allow_unicode=True)
    
    print(f"\nConfiguration has been saved to {filename}")

if __name__ == "__main__":
    main()