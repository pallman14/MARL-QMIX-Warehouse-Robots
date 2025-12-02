from PIL import Image
import os

# Episode counts and order matters
EPISODE_COUNTS = [5001, 1751]  # 5001 first

# Directory to save comparison images
OUTPUT_DIR = "comparison"
os.makedirs(OUTPUT_DIR, exist_ok=True)

# Function to combine two images vertically
def combine_images_vertical(img1_path, img2_path, output_path):
    # This function expects full paths
    img1 = Image.open(img1_path)
    img2 = Image.open(img2_path)

    max_width = max(img1.width, img2.width)
    total_height = img1.height + img2.height

    new_img = Image.new('RGB', (max_width, total_height), color=(255, 255, 255))
    new_img.paste(img1, (0, 0))
    new_img.paste(img2, (0, img1.height))
    new_img.save(output_path)
    print(f"Saved vertical comparison image: {output_path}")

# Folder paths
folder1 = os.path.join(os.getcwd(), str(EPISODE_COUNTS[0]))
folder2 = os.path.join(os.getcwd(), str(EPISODE_COUNTS[1]))

# Find all PNG files in the first episode folder
png_files1 = [f for f in os.listdir(folder1) if f.endswith(".png")]
png_files2 = [f for f in os.listdir(folder2) if f.endswith(".png")]

# Match files by name (ignoring the episode prefix)
for file1 in png_files1:
    base_name = "_".join(file1.split("_")[1:])  # remove "5001_" prefix
    file2 = f"{EPISODE_COUNTS[1]}_{base_name}"

    if file2 in png_files2:
        # Full paths are created here:
        img1_path = os.path.join(folder1, file1)
        img2_path = os.path.join(folder2, file2)
        output_file = os.path.join(OUTPUT_DIR, f"compare_{base_name}")
        
        # Use the full path variables
        combine_images_vertical(img1_path, img2_path, output_file) 
    else:
        print(f"Warning: {file2} not found in folder {folder2}")