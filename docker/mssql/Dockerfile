FROM mcr.microsoft.com/mssql/server:2022-latest

COPY docker/mssql/init-db.sh /scripts/init-db.sh
COPY docker/mssql/restore.env.example /scripts/restore.env.example

USER root
RUN sed -i 's/\r$//' /scripts/init-db.sh /scripts/restore.env.example && chmod +x /scripts/init-db.sh
USER mssql

ENTRYPOINT ["/bin/bash", "/scripts/init-db.sh"]
