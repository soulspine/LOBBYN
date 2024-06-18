import os
import json

# Specify the directory path
directory = os.getcwd()

# Iterate over all files in the directory
for filename in os.listdir(directory):
    if filename.endswith('.json'):
        # Open the JSON file
        with open(os.path.join(directory, filename), 'r') as file:
            # Load the JSON data
            data = json.load(file)

        # Save the JSON file with indent = 4
        with open(os.path.join(directory, filename), 'w') as file:
            json.dump(data, file, indent=4)