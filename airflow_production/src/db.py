FROM apache/airflow:2.10.4

USER root

# 1. Install System Dependencies & Tools
RUN apt-get update && apt-get install -y \
    gcc \
    g++ \
    unixodbc-dev \
    curl

# 2. Install ODBC Driver SQL Server (JANGAN DIHAPUS, INI PENTING!)
# Ini driver jembatan biar Python bisa ngomong sama SQL Server kantor
RUN curl https://packages.microsoft.com/keys/microsoft.asc | apt-key add - && \
    curl https://packages.microsoft.com/config/debian/12/prod.list > /etc/apt/sources.list.d/mssql-release.list && \
    apt-get update && ACCEPT_EULA=Y apt-get install -y msodbcsql18

USER airflow

# 3. Copy file requirements.txt (Cara Advance/Rapi)
# Pastikan file requirements.txt sudah kamu buat di folder yang sama dengan Dockerfile
COPY requirements.txt /requirements.txt

# 4. Install Library Python dari file text tadi
RUN pip install --no-cache-dir -r /requirements.txt