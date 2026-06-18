FROM mcr.microsoft.com/mssql/server:2022-latest

COPY docker/mssql/entrypoint.sh /scripts/entrypoint.sh
COPY docker/mssql/restore-db.sh /scripts/restore-db.sh

USER root
RUN sed -i 's/\r$//' /scripts/entrypoint.sh /scripts/restore-db.sh \
    && chmod +x /scripts/entrypoint.sh /scripts/restore-db.sh
USER mssql

ENTRYPOINT ["/bin/bash", "/scripts/entrypoint.sh"]
