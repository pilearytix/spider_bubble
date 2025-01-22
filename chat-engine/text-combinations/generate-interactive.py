import json
import random
import keyboard
import time
import os

def clear_console():
    os.system('cls' if os.name == 'nt' else 'clear')

def load_conversation_data(file_path):
    with open(file_path, 'r') as file:
        return json.load(file)

def generate_question(data):
    # Select a random object
    object_key = random.choice(list(data["objects"].keys()))
    object_data = data["objects"][object_key]
    
    # Select random components
    observation = random.choice(object_data["observations"])
    cause = random.choice(object_data["causes"])
    mystical_question = random.choice(object_data["mystical_questions"])
    
    return data["question_structure"].replace(
        "[observation]", observation
    ).replace(
        "[cause]", cause
    ).replace(
        "[mystical_question]", mystical_question
    ), object_key

def generate_response1(data, object_key, direction="left"):
    acknowledgement = random.choice(data["responses"]["acknowledgements"])
    outcomes = data["responses"]["outcomes"][object_key]
    outcome = random.choice(outcomes[direction])
    speculation = random.choice(data["responses"]["speculations"])
    
    # Get the related items from the outcome's direction
    valid_items = outcomes[f"{direction}_items"]
    
    return f"Ah, {acknowledgement} {outcome} {speculation}", valid_items

def generate_response2(data, valid_items, action_type=None, item_key=None):
    # Filter actions to only include those with valid items from R1's outcome
    available_actions = {}
    for action_type_key, items in data["actions"].items():
        valid_action_items = {
            k: v for k, v in items.items() 
            if k in valid_items
        }
        if valid_action_items:
            available_actions[action_type_key] = valid_action_items
    
    if not available_actions:
        return "I'm not sure what else to suggest for this situation."
    
    # Choose action type if not provided or if current one is invalid
    if action_type is None or action_type not in available_actions:
        action_type = random.choice(list(available_actions.keys()))
    
    # Get valid items for the selected action type
    valid_items_for_type = available_actions[action_type]
    
    # Select random item if not provided or if current one is invalid
    if item_key is None or item_key not in valid_items_for_type:
        item_key = random.choice(list(valid_items_for_type.keys()))
    
    item_data = valid_items_for_type[item_key]
    action = random.choice(item_data["actions"])
    result = random.choice(item_data["valid_results"])
    
    return f"Maybe you should {action} {item_data['name']}, {result}"

def interactive_generation():
    data = load_conversation_data('text-combinations/data.json')
    current_text = ""
    stage = 0
    current_object_key = None
    current_valid_items = None
    current_direction = "left"
    current_action_type = None
    current_item_key = None
    
    print("Use UP ARROW to regenerate current line.")
    print("Use LEFT/RIGHT ARROWS to change outcome direction (for Response 1).")
    print("Use TAB to cycle through action types, SPACE to cycle through items (for Response 2).")
    print("Press ENTER to proceed to next line.")
    print("Press ESC to exit.\n")
    
    while True:
        clear_console()
        print("Use UP ARROW to regenerate current line.")
        print("Use LEFT/RIGHT ARROWS to change outcome direction (for Response 1).")
        print("Use TAB to cycle through action types, SPACE to cycle through items (for Response 2).")
        print("Press ENTER to proceed to next line.")
        print("Press ESC to exit.\n")
        
        # Print previous stages if they exist
        if stage >= 1:
            print("Q:", current_question)
        if stage >= 2:
            print("R1:", current_response1)
        
        # Generate and display current stage
        if stage == 0:
            current_text, current_object_key = generate_question(data)
            print("Q:", current_text)
        elif stage == 1:
            current_text, current_valid_items = generate_response1(data, current_object_key, current_direction)
            print("R1:", current_text)
            print(f"Direction: {current_direction}")
        elif stage == 2:
            current_text = generate_response2(data, current_valid_items, current_action_type, current_item_key)
            print("R2:", current_text)
            if current_action_type:
                print(f"Action Type: {current_action_type}")
            if current_item_key:
                print(f"Item: {current_item_key}")
        
        # Wait for keyboard input
        while True:
            if keyboard.is_pressed('up'):
                time.sleep(0.2)
                if stage == 2:
                    current_action_type = None
                    current_item_key = None
                break
            elif keyboard.is_pressed('left') and stage == 1:
                time.sleep(0.2)
                current_direction = "left"
                current_text, current_valid_items = generate_response1(data, current_object_key, current_direction)
                clear_console()
                print("Q:", current_question)
                print("R1:", current_text)
                print(f"Direction: {current_direction}")
            elif keyboard.is_pressed('right') and stage == 1:
                time.sleep(0.2)
                current_direction = "right"
                current_text, current_valid_items = generate_response1(data, current_object_key, current_direction)
                clear_console()
                print("Q:", current_question)
                print("R1:", current_text)
                print(f"Direction: {current_direction}")
            elif keyboard.is_pressed('tab') and stage == 2:
                time.sleep(0.2)
                available_actions = {
                    action_type_key: items 
                    for action_type_key, items in data["actions"].items()
                    if any(k in current_valid_items for k, v in items.items())
                }
                action_types = list(available_actions.keys())
                if not action_types:
                    continue
                
                if current_action_type is None:
                    current_action_type = action_types[0]
                else:
                    current_index = action_types.index(current_action_type)
                    current_action_type = action_types[(current_index + 1) % len(action_types)]
                current_item_key = None
                current_text = generate_response2(data, current_valid_items, current_action_type, current_item_key)
                clear_console()
                print("Q:", current_question)
                print("R1:", current_response1)
                print("R2:", current_text)
                print(f"Action Type: {current_action_type}")
            elif keyboard.is_pressed('space') and stage == 2 and current_action_type:
                time.sleep(0.2)
                valid_items_for_type = {
                    k: v for k, v in data["actions"][current_action_type].items()
                    if k in current_valid_items
                }
                items = list(valid_items_for_type.keys())
                if not items:
                    continue
                
                if current_item_key is None:
                    current_item_key = items[0]
                else:
                    current_index = items.index(current_item_key)
                    current_item_key = items[(current_index + 1) % len(items)]
                current_text = generate_response2(data, current_valid_items, current_action_type, current_item_key)
                clear_console()
                print("Q:", current_question)
                print("R1:", current_response1)
                print("R2:", current_text)
                print(f"Action Type: {current_action_type}")
                print(f"Item: {current_item_key}")
            elif keyboard.is_pressed('enter'):
                time.sleep(0.2)
                if stage == 0:
                    current_question = current_text
                    stage = 1
                elif stage == 1:
                    current_response1 = current_text
                    stage = 2
                elif stage == 2:
                    print("\nConversation complete! Press ESC to exit or ENTER to start new conversation.")
                    if keyboard.read_event(suppress=True).name == 'esc':
                        return
                    stage = 0
                    current_direction = "left"
                    current_action_type = None
                    current_item_key = None
                break
            elif keyboard.is_pressed('esc'):
                return

if __name__ == "__main__":
    try:
        interactive_generation()
    except KeyboardInterrupt:
        print("\nExiting...") 