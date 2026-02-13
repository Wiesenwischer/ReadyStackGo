#!/bin/sh
set -e

OLD="${OLD_CONTAINER:?OLD_CONTAINER env var required}"
NEW="${NEW_CONTAINER:?NEW_CONTAINER env var required}"

echo "=== RSGO Self-Update ==="
echo "Old container: $OLD"
echo "New container: $NEW"

echo "=== Stopping $OLD ==="
docker stop "$OLD" --time 10

echo "=== Serving update page on :8080 ==="
busybox httpd -f -p 8080 -h /www &
HTTP_PID=$!

echo "=== Removing old container ==="
docker rm "$OLD"

echo "=== Renaming $NEW -> $OLD ==="
docker rename "$NEW" "$OLD"

echo "=== Stopping update page, starting new container ==="
kill $HTTP_PID 2>/dev/null; wait $HTTP_PID 2>/dev/null || true
docker start "$OLD"

echo "=== Waiting for health check ==="
for i in $(seq 1 30); do
  STATUS=$(docker inspect --format='{{.State.Health.Status}}' "$OLD" 2>/dev/null || echo "unknown")
  if [ "$STATUS" = "healthy" ]; then
    echo "=== Update complete! New container is healthy ==="
    exit 0
  fi
  sleep 2
done

echo "=== Warning: health check timeout, but container is running ==="
