# Claude's Notes for ReadyStackGo

This file contains preferences and learnings specific to this project that I should remember across sessions.

## Commit Message Style

**IMPORTANT**: Do NOT include Claude attribution in commit messages.

- ❌ Do NOT add: "🤖 Generated with [Claude Code](https://claude.com/claude-code)"
- ❌ Do NOT add: "Co-Authored-By: Claude <noreply@anthropic.com>"
- ✅ Write clean, professional commit messages without attribution footers

Example of correct commit message:
```
Fix RemoveStack endpoint to return proper HTTP status

- Changed RemoveStackEndpoint to use EndpointWithoutRequest pattern
- FastEndpoints automatically returns 204 No Content for empty responses
- Updated integration tests to expect 204 No Content instead of 200 OK
- Removed unused RemoveStackRequest class
- All 24 integration tests now passing
```

## Testing Standards

- ALL tests must pass before committing - no exceptions for port conflicts or environment issues
- Tests must actually catch bugs, not just check status codes
- Integration tests are critical for this project (Docker interactions)
- Use dynamic port allocation in Testcontainers to avoid conflicts

## Project-Specific Learnings

### Docker Container Cleanup
- Always check for and remove existing containers with the same name before creating new ones
- Use `Force = true` when removing containers to handle both stopped and running containers
- Container name conflicts are a common issue in stack redeployment scenarios

### FastEndpoints Patterns
- Use `EndpointWithoutRequest` for endpoints that only use route parameters
- FastEndpoints automatically returns 204 No Content for empty responses
- DELETE operations typically return 204 No Content, not 200 OK

### Architecture
- Clean Architecture with Dispatcher pattern (not MediatR)
- Business logic belongs in Handlers, not in Endpoints
- Service interfaces in Application/Domain, implementations in Infrastructure
