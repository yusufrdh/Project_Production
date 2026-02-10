from airflow import DAG
from airflow.operators.python import PythonOperator
from airflow.providers.microsoft.mssql.hooks.mssql import MsSqlHook
import pandas as pd
from datetime import datetime, timedelta
import os
import re

CONN_ID = 'mssql_production'

def get_lookup_data():
    hook = MsSqlHook(mssql_conn_id=CONN_ID)
    
    # 1. Product Map (Wajib buat CM)
    df_prod = hook.get_pandas_df("SELECT product_name, id_product FROM dbo.coal")
    product_map = dict(zip(df_prod['product_name'], df_prod['id_product']))
    
    # 2. Pit Map (Whitelist)
    pit_map = {}
    df_off = hook.get_pandas_df("SELECT pit_name_official, id_pit FROM dbo.pit_official")
    pit_map.update(dict(zip(df_off['pit_name_official'], df_off['id_pit'])))
    df_alias = hook.get_pandas_df("SELECT alias_name, id_pit FROM dbo.pit_alias")
    pit_map.update(dict(zip(df_alias['alias_name'], df_alias['id_pit'])))
    
    return product_map, pit_map

# [FIX 1] Nama fungsi disamakan dengan pemanggil di DAG
def proses_etl_cm():
    # --- ARAHKAN KE FOLDER KHUSUS CM ---
    folder_path = '/opt/airflow/data_kpc/CM'
    
    if not os.path.exists(folder_path):
        raise FileNotFoundError(f"❌ Folder {folder_path} TIDAK DITEMUKAN!")

    all_files = [f for f in os.listdir(folder_path) if f.endswith('.xlsx') and not f.startswith('~$')]
    
    if not all_files:
        print("⚠️ Tidak ada file Excel di folder CM.")
        return

    hook = MsSqlHook(mssql_conn_id=CONN_ID)
    engine = hook.get_sqlalchemy_engine()

    print("🧹 Truncate Table CM (Actual & Plan)...")
    hook.run("TRUNCATE TABLE dbo.cm_actual")
    # hook.run("TRUNCATE TABLE dbo.cm_plan") 
    
    product_map, pit_map = get_lookup_data()

    for nama_file in all_files:
        full_path = f'{folder_path}/{nama_file}'
        print(f"\n📄 PROSES FILE CM: {nama_file}")
        
        try:
            xls = pd.ExcelFile(full_path)
        except Exception as e:
            print(f"❌ Error: {e}")
            continue
        
        df_actual_total = pd.DataFrame()
        df_plan_excel = pd.DataFrame()

        for sheet_name in xls.sheet_names:
            
            # 1. BERSIHKAN NAMA SHEET 
            clean_name = re.sub(r'^(CM|OB|CE)\s*', '', sheet_name, flags=re.IGNORECASE).strip()
            
            # 2. CEK WHITELIST DB 
            match_name = None
            if sheet_name in pit_map: match_name = sheet_name
            elif clean_name in pit_map: match_name = clean_name
            
            if not match_name: continue 
            
            current_pit_id = pit_map[match_name]
            

            try:
                df = pd.read_excel(xls, sheet_name=sheet_name, header=10)
            except: continue

            df.columns = df.columns.astype(str).str.strip()
            if not {'Date', 'Month', 'Year'}.issubset(df.columns): continue

            # Parsing Tanggal
            try:
                df['Date_Str'] = (
                    df['Date'].astype(str).str.replace('.0', '', regex=False) + '-' + 
                    df['Month'].astype(str).str.replace('.0', '', regex=False) + '-' + 
                    df['Year'].astype(str).str.replace('.0', '', regex=False)
                )
                df['tgl'] = pd.to_datetime(df['Date_Str'], format='%d-%b-%Y', errors='coerce')
                df = df.dropna(subset=['tgl'])
            except: continue

            # A. AMBIL ACTUAL (Mapping Product)
            actual_cols = [col for col in df.columns if col in product_map]
            if actual_cols:
                df_act = df[['tgl'] + actual_cols].melt(id_vars=['tgl'], value_vars=actual_cols, var_name='prod', value_name='qty')
                df_act['id_product'] = df_act['prod'].map(product_map)
                df_act['id_pit'] = current_pit_id
                df_act['data_provenance'] = f'{nama_file}|{sheet_name}'
                df_act['created_at'] = datetime.now()
                df_act['actual_qty'] = pd.to_numeric(df_act['qty'], errors='coerce').fillna(0)
                df_actual_total = pd.concat([df_actual_total, df_act])

            # B. AMBIL PLAN
            plan_mapping = {}
            for col in df.columns:
                match = re.match(r"(.+)\.1$", col)
                if match:
                    clean_name_prod = match.group(1).strip()
                    if clean_name_prod in product_map:
                        plan_mapping[col] = clean_name_prod
            
            plan_cols = list(plan_mapping.keys())
            if plan_cols:
                df_pln = df[['tgl'] + plan_cols].melt(id_vars=['tgl'], value_vars=plan_cols, var_name='col_excel', value_name='qty')
                df_pln['id_product'] = df_pln['col_excel'].map(plan_mapping).map(product_map)
                df_pln['id_pit'] = current_pit_id
                df_pln['data_provenance'] = f'{nama_file}|{sheet_name}'
                df_pln['plan_qty'] = pd.to_numeric(df_pln['qty'], errors='coerce').fillna(0)
                df_plan_excel = pd.concat([df_plan_excel, df_pln])

        # UPLOAD KE DB
        if not df_actual_total.empty:
            cols = ['id_pit', 'id_product', 'production_date', 'actual_qty', 'data_provenance', 'created_at']
            df_actual_total = df_actual_total.rename(columns={'tgl': 'production_date'})
            df_actual_total[cols].to_sql('cm_actual', con=engine, if_exists='append', index=False, chunksize=1000)
            print(f"   ✅ Inserted {len(df_actual_total)} rows to cm_actual.")

        if not df_plan_excel.empty:
            proses_versioning_cm(df_plan_excel, hook, engine) 

    print("\n✅ SEMUA FILE CM SELESAI!")

def proses_versioning_cm(df_new, hook, engine):
    print("   🔍 Checking Versioning for Plan...")
    sql_existing = """
    SELECT id_pit, id_product, plan_date, plan_qty as db_qty, version as db_ver
    FROM dbo.cm_plan WHERE is_active = 1
    """
    df_db = hook.get_pandas_df(sql_existing)
    
    df_new['tgl'] = pd.to_datetime(df_new['tgl'])
    df_db['plan_date'] = pd.to_datetime(df_db['plan_date'])

    merged = pd.merge(
        df_new, df_db, 
        left_on=['id_pit', 'id_product', 'tgl'], 
        right_on=['id_pit', 'id_product', 'plan_date'], 
        how='left', indicator=True
    )

    new_records = merged[merged['_merge'] == 'left_only'].copy()
    if not new_records.empty:
        new_records['plan_date'] = new_records['tgl']
        new_records['version'] = 1
        new_records['is_active'] = 1
        new_records['created_at'] = datetime.now()
        cols = ['id_pit', 'id_product', 'plan_date', 'plan_qty', 'version', 'is_active', 'data_provenance', 'created_at']
        new_records[cols].to_sql('cm_plan', con=engine, if_exists='append', index=False, chunksize=1000)

    changed = merged[(merged['_merge'] == 'both') & (abs(merged['plan_qty'] - merged['db_qty']) > 0.01)].copy()
    if not changed.empty:
        changed['plan_date'] = changed['tgl']
        conn = hook.get_conn()
        cursor = conn.cursor()
        for i, row in changed.iterrows():
            tgl_sql = row['tgl'].strftime('%Y-%m-%d')
            sql = f"UPDATE dbo.cm_plan SET is_active=0 WHERE id_pit={row['id_pit']} AND id_product={row['id_product']} AND plan_date='{tgl_sql}' AND is_active=1"
            cursor.execute(sql)
        conn.commit()
        changed['version'] = changed['db_ver'] + 1
        changed['is_active'] = 1
        changed['created_at'] = datetime.now()
        cols = ['id_pit', 'id_product', 'plan_date', 'plan_qty', 'version', 'is_active', 'data_provenance', 'created_at']
        changed[cols].to_sql('cm_plan', con=engine, if_exists='append', index=False, chunksize=1000)

default_args = {
    'owner': 'airflow',
    'retries': 0,
    'retry_delay': timedelta(minutes=5),
}

with DAG(
    dag_id='etl_cm', 
    default_args=default_args,
    description='Ingest CM Data (Folder Isolated)',
    start_date=datetime(2024, 1, 1),
    schedule_interval=None,
    catchup=False,
) as dag:
    task_ingest = PythonOperator(
        task_id='process_smart_etl',
        python_callable=proses_etl_cm
    )