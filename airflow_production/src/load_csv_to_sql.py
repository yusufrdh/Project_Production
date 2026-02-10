import pandas as pd
from pathlib import Path
from db import get_engine
import re

DATA_DIR = Path("data")

def normalize_table_name(filename):
    name = filename.stem.lower()
    name = re.sub(r"[^a-z0-9]+", "_", name)
    return f"raw_{name}"

def load_csv():
    engine = get_engine()

    for csv_file in DATA_DIR.glob("*.csv"):
        table_name = normalize_table_name(csv_file)

        df = pd.read_csv(csv_file)

        df.to_sql(
            name=table_name,
            con=engine,
            if_exists="replace",
            index=False
        )

        print(f"✅ {csv_file.name} → table {table_name}")

if __name__ == "__main__":
    load_csv()
