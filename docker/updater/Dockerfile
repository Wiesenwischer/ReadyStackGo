FROM docker:cli

COPY index.html /www/index.html
COPY update.sh /update.sh
RUN chmod +x /update.sh

ENTRYPOINT ["/bin/sh", "/update.sh"]
