FROM apache/airflow:2.10.4

# 1. Masuk mode Root buat install Driver System
USER root

RUN apt-get update && apt-get install -y --no-install-recommends \
    gcc \
    g++ \
    unixodbc-dev \
    curl \
    gnupg2

# Install Driver MSSQL (ODBC 18)
RUN curl https://packages.microsoft.com/keys/microsoft.asc | apt-key add - && \
    curl https://packages.microsoft.com/config/debian/12/prod.list > /etc/apt/sources.list.d/mssql-release.list && \
    apt-get update && ACCEPT_EULA=Y apt-get install -y msodbcsql18

# 2. PENTING: Kembali ke User Airflow
# Kalau baris ini hilang, Airflow bakal error "Command not found"
USER airflow

# 3. Install Library Python Langsung Disini
RUN pip install --no-cache-dir \
    apache-airflow-providers-microsoft-mssql \
    pandas \
    openpyxl \
    pyodbc