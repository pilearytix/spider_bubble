import json
import random

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

def generate_response1(data, object_key):
    acknowledgement = random.choice(data["responses"]["acknowledgements"])
    direction = random.choice(["left", "right"])
    outcome = random.choice(data["responses"]["outcomes"][object_key][direction])
    speculation = random.choice(data["responses"]["speculations"])
    
    return f"Ah, {acknowledgement} {outcome} {speculation}"

def generate_response2(data):
    # Choose action type (objects, npcs, or locations)
    action_type = random.choice(list(data["actions"].keys()))
    
    # Select random item from the chosen type
    items = data["actions"][action_type]
    item_key = random.choice(list(items.keys()))
    item_data = items[item_key]
    
    # Get action and result for the specific item
    action = random.choice(item_data["actions"])
    result = random.choice(item_data["valid_results"])
    
    return f"Maybe you should {action} {item_data['name']}, {result}"

def generate_conversation():
    data = load_conversation_data('text-combinations/data.json')
    
    question, object_key = generate_question(data)
    response1 = generate_response1(data, object_key)
    response2 = generate_response2(data)
    
    return {
        "Question": question,
        "Response1": response1,
        "Response2": response2
    }

# Generate and print a conversation
if __name__ == "__main__":
    conversation = generate_conversation()
    print("\nGenerated Conversation:")
    print("-" * 50)
    print("Q:", conversation["Question"])
    print("R1:", conversation["Response1"])
    print("R2:", conversation["Response2"])
