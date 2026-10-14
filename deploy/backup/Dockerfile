# Backup job (GL-08): pg_dump + rclone. Encryption is rclone's crypt remote (NaCl secretbox, scrypt-derived key), so
# the off-site provider only ever holds ciphertext, for the database dumps and the blob copies alike.
FROM alpine:3.22

RUN apk add --no-cache postgresql17-client rclone ca-certificates tzdata \
    && addgroup -S backup && adduser -S -G backup -h /tmp backup

COPY backup.sh restore.sh /usr/local/bin/
RUN chmod 0755 /usr/local/bin/backup.sh /usr/local/bin/restore.sh

USER backup
ENV HOME=/tmp
ENTRYPOINT ["/usr/local/bin/backup.sh"]
CMD ["schedule"]
