#!/bin/sh

OLD="${OLD_CONTAINER:?OLD_CONTAINER env var required}"
NEW="${NEW_CONTAINER:?NEW_CONTAINER env var required}"
BACKUP="${OLD}-backup"

echo "=== RSGO Self-Update ==="
echo "Old container: $OLD"
echo "New container: $NEW"

# Stop old container (fail = abort, nothing to rollback yet)
echo "=== Stopping $OLD ==="
docker stop "$OLD" --time 10 || { echo "ERROR: Could not stop old container"; exit 1; }

# Serve maintenance page while updating
echo "=== Serving update page on :8080 ==="
busybox httpd -f -p 8080 -h /www &
HTTP_PID=$!

# Rename old to backup (fail = abort, just restart old)
echo "=== Renaming $OLD -> $BACKUP ==="
if ! docker rename "$OLD" "$BACKUP"; then
  echo "ERROR: Could not rename old container, restarting it"
  kill $HTTP_PID 2>/dev/null; wait $HTTP_PID 2>/dev/null || true
  docker start "$OLD"
  exit 1
fi

# Rename new to old name (fail = restore backup)
echo "=== Renaming $NEW -> $OLD ==="
if ! docker rename "$NEW" "$OLD"; then
  echo "ERROR: Could not rename new container, restoring backup"
  docker rename "$BACKUP" "$OLD"
  kill $HTTP_PID 2>/dev/null; wait $HTTP_PID 2>/dev/null || true
  docker start "$OLD"
  exit 1
fi

# Start the new container (keep maintenance page running until health check passes)
echo "=== Starting new container ==="
if ! docker start "$OLD"; then
  echo "ERROR: New container failed to start, rolling back"
  docker rm "$OLD" 2>/dev/null || true
  docker rename "$BACKUP" "$OLD"
  kill $HTTP_PID 2>/dev/null; wait $HTTP_PID 2>/dev/null || true
  docker start "$OLD"
  echo "=== Rollback complete. Old version restored. ==="
  exit 1
fi

# Wait for health check
echo "=== Waiting for health check ==="
HEALTHY=false
for i in $(seq 1 30); do
  STATUS=$(docker inspect --format='{{.State.Health.Status}}' "$OLD" 2>/dev/null || echo "unknown")
  if [ "$STATUS" = "healthy" ]; then
    HEALTHY=true
    break
  fi
  # Accept "running" when no healthcheck is defined
  RUNNING=$(docker inspect --format='{{.State.Running}}' "$OLD" 2>/dev/null || echo "false")
  if [ "$STATUS" = "none" ] && [ "$RUNNING" = "true" ]; then
    HEALTHY=true
    break
  fi
  # Detect stopped/crashed container early
  if [ "$RUNNING" = "false" ]; then
    echo "ERROR: New container stopped unexpectedly"
    break
  fi
  sleep 2
done

# Stop maintenance page
kill $HTTP_PID 2>/dev/null; wait $HTTP_PID 2>/dev/null || true

if [ "$HEALTHY" = "true" ]; then
  echo "=== Update complete! New container is healthy ==="
  docker rm "$BACKUP" 2>/dev/null || true
  exit 0
fi

# Rollback
echo "=== ERROR: New container failed health check, rolling back ==="
docker stop "$OLD" --time 5 2>/dev/null || true
docker rm "$OLD" 2>/dev/null || true
docker rename "$BACKUP" "$OLD"
docker start "$OLD"
echo "=== Rollback complete. Old version restored. ==="
exit 1
