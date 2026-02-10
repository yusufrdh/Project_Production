from airflow import DAG
from airflow.operators.python import PythonOperator
from airflow.providers.microsoft.mssql.hooks.mssql import MsSqlHook
import pandas as pd
from datetime import datetime, timedelta
import os
import re

CONN_ID = 'mssql_production'

def get_pit_map():
    hook = MsSqlHook(mssql_conn_id=CONN_ID)
    pit_map = {}
    
    # Ambil Mapping Pit Resmi & Alias dari DB
    df_off = hook.get_pandas_df("SELECT pit_name_official, id_pit FROM dbo.pit_official")
    pit_map.update(dict(zip(df_off['pit_name_official'], df_off['id_pit'])))
    
    df_alias = hook.get_pandas_df("SELECT alias_name, id_pit FROM dbo.pit_alias")
    pit_map.update(dict(zip(df_alias['alias_name'], df_alias['id_pit'])))
    
    return pit_map

def proses_etl_ob():
    folder_path = '/opt/airflow/data_kpc/OB'
    
    if not os.path.exists(folder_path):
        raise FileNotFoundError(f"❌ Folder {folder_path} TIDAK DITEMUKAN!")

    all_files = [f for f in os.listdir(folder_path) if f.endswith('.xlsx') and not f.startswith('~$')]
    
    if not all_files:
        print("⚠️ Tidak ada file Excel di folder OB.")
        return

    hook = MsSqlHook(mssql_conn_id=CONN_ID)
    engine = hook.get_sqlalchemy_engine()

    # Reset Data Actual OB
    print("🧹 Truncate Table OB Actual...")
    hook.run("TRUNCATE TABLE dbo.ob_actual")
    
    pit_map = get_pit_map()

    for nama_file in all_files:
        full_path = f'{folder_path}/{nama_file}'
        print(f"\n📄 PROSES FILE OB: {nama_file}")
        
        try:
            xls = pd.ExcelFile(full_path)
        except Exception as e:
            print(f"❌ Error baca file: {e}")
            continue
        
        df_actual_total = pd.DataFrame()
        df_plan_excel = pd.DataFrame()

        for sheet_name in xls.sheet_names:
            
            # --- [LOGIC 1] SAFETY NET: CEK SALAH KAMAR ---
            if re.match(r'^(CM|CE|CP)\s*', sheet_name, re.IGNORECASE):
                # print(f"   ⏩ SKIP: Sheet '{sheet_name}' bukan OB (Salah Folder?).")
                continue

            # --- [LOGIC 2] CLEANING NAMA SHEET ---
            clean_name = re.sub(r'^OB\s*', '', sheet_name, flags=re.IGNORECASE).strip()
            
            # --- [LOGIC 3] DYNAMIC MAPPING ---
            match_name = None
            if sheet_name in pit_map: match_name = sheet_name
            elif clean_name in pit_map: match_name = clean_name
            
            if not match_name: continue 
            
            current_pit_id = pit_map[match_name]

            # --- BACA DATA 
            try:
                df = pd.read_excel(xls, sheet_name=sheet_name, header=10)
            except: continue

            df.columns = df.columns.astype(str).str.strip()
            # Validasi minimal kolom tanggal
            if not {'Date', 'Month', 'Year'}.issubset(df.columns): continue

            # Parsing Tanggal Standard
            try:
                df['Date_Str'] = (
                    df['Date'].astype(str).str.replace('.0', '', regex=False) + '-' + 
                    df['Month'].astype(str).str.replace('.0', '', regex=False) + '-' + 
                    df['Year'].astype(str).str.replace('.0', '', regex=False)
                )
                df['tgl'] = pd.to_datetime(df['Date_Str'], format='%d-%b-%Y', errors='coerce')
                df = df.dropna(subset=['tgl'])
            except: continue

            # A. AMBIL ACTUAL 
            act_col = next((col for col in df.columns if col.lower() == 'actual'), None)
            if act_col:
                df_act = df[['tgl', act_col]].copy().rename(columns={act_col: 'actual_qty'})
                df_act['id_pit'] = current_pit_id
                df_act['data_provenance'] = f'{nama_file}|{sheet_name}'
                df_act['created_at'] = datetime.now()
                df_act['actual_qty'] = pd.to_numeric(df_act['actual_qty'], errors='coerce').fillna(0)
                df_actual_total = pd.concat([df_actual_total, df_act])

            # B. AMBIL PLAN 
            pln_col = next((col for col in df.columns if col.lower() == 'plan'), None)
            if pln_col:
                df_pln = df[['tgl', pln_col]].copy().rename(columns={pln_col: 'plan_qty'})
                df_pln['id_pit'] = current_pit_id
                df_pln['data_provenance'] = f'{nama_file}|{sheet_name}'
                df_pln['plan_qty'] = pd.to_numeric(df_pln['plan_qty'], errors='coerce').fillna(0)
                df_plan_excel = pd.concat([df_plan_excel, df_pln])

        # UPLOAD KE DB
        if not df_actual_total.empty:
            cols = ['id_pit', 'production_date', 'actual_qty', 'data_provenance', 'created_at']
            df_actual_total = df_actual_total.rename(columns={'tgl': 'production_date'})
            # TARGET: dbo.ob_actual
            df_actual_total[cols].to_sql('ob_actual', con=engine, if_exists='append', index=False, chunksize=1000)
            print(f"   ✅ Inserted {len(df_actual_total)} rows to ob_actual.")

        if not df_plan_excel.empty:
            proses_versioning_ob(df_plan_excel, hook, engine)

    print("\n✅ SEMUA FILE OB SELESAI!")

def proses_versioning_ob(df_new, hook, engine):
    print("   🔍 Checking Versioning for Plan OB...")
    # TARGET: dbo.ob_plan
    sql_existing = "SELECT id_pit, plan_date, plan_qty as db_qty, version as db_ver FROM dbo.ob_plan WHERE is_active = 1"
    df_db = hook.get_pandas_df(sql_existing)
    
    df_new['tgl'] = pd.to_datetime(df_new['tgl'])
    df_db['plan_date'] = pd.to_datetime(df_db['plan_date'])

    merged = pd.merge(df_new, df_db, left_on=['id_pit', 'tgl'], right_on=['id_pit', 'plan_date'], how='left', indicator=True)

    # Insert Ver 1
    new_records = merged[merged['_merge'] == 'left_only'].copy()
    if not new_records.empty:
        new_records['plan_date'] = new_records['tgl']
        new_records['version'] = 1
        new_records['is_active'] = 1
        new_records['created_at'] = datetime.now()
        cols = ['id_pit', 'plan_date', 'plan_qty', 'version', 'is_active', 'data_provenance', 'created_at']
        new_records[cols].to_sql('ob_plan', con=engine, if_exists='append', index=False, chunksize=1000)

    # Update Version Naik
    changed = merged[(merged['_merge'] == 'both') & (abs(merged['plan_qty'] - merged['db_qty']) > 0.01)].copy()
    if not changed.empty:
        changed['plan_date'] = changed['tgl']
        conn = hook.get_conn()
        cursor = conn.cursor()
        for i, row in changed.iterrows():
            tgl_sql = row['tgl'].strftime('%Y-%m-%d')
            sql = f"UPDATE dbo.ob_plan SET is_active=0 WHERE id_pit={row['id_pit']} AND plan_date='{tgl_sql}' AND is_active=1"
            cursor.execute(sql)
        conn.commit()
        changed['version'] = changed['db_ver'] + 1
        changed['is_active'] = 1
        changed['created_at'] = datetime.now()
        cols = ['id_pit', 'plan_date', 'plan_qty', 'version', 'is_active', 'data_provenance', 'created_at']
        changed[cols].to_sql('ob_plan', con=engine, if_exists='append', index=False, chunksize=1000)

default_args = {
    'owner': 'airflow',
    'retries': 0,
    'retry_delay': timedelta(minutes=5),
}

with DAG(
    dag_id='etl_ob', 
    default_args=default_args,
    description='Ingest OB Data (Folder Isolated)',
    start_date=datetime(2024, 1, 1),
    schedule_interval=None,
    catchup=False,
) as dag:
    task_ingest = PythonOperator(
        task_id='process_ob',
        python_callable=proses_etl_ob
    )