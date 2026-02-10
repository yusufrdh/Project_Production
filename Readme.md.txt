# KP Mining Integration System

## Cara Install (Deployment)

1. Pastikan Docker sudah terinstall.
2. Jalankan perintah:
   docker-compose up -d

## Cara Restore Database (PENTING!)

Setelah container jalan, jalankan perintah ini di terminal untuk mengisi database:

docker exec -i sqlserver_prod /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P 'ProductionKPC' -d master -i /usr/src/db_setup/db_production.sql

## Akses Aplikasi

- **Web Mapping:** http://localhost:8081
- **Airflow:** http://localhost:8080 (Login: airflow/airflow)