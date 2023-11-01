import subprocess
import csv

num_nodes = [x for x in range(500, 5000, 500)]
req = 5
output_csv = "result.csv"

with open(output_csv, mode="w", newline="") as csv_file:
    csv_writer = csv.writer(csv_file)
    csv_writer.writerow(["Input", "Output"])

    for node in num_nodes:
        print(f"Input: '{node} {req}'")
        result = subprocess.run(
            ["dotnet", "run", str(node), str(req)],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        )
        if result.returncode == 0:
            output = result.stdout.strip()
            print(f"Output: {output}")
            csv_writer.writerow([node, output])
        else:
            print(f"Error: {result.stderr.strip()}")

print(f"Output saved to {output_csv}")
