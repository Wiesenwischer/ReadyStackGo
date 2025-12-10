Feature: Environment Management
    As an organization admin
    I want to manage Docker environments
    So that I can deploy stacks to different targets

Background:
    Given organization "ACME Corp" exists

Scenario: Create a new environment with Docker socket
    When I create environment "local" for "ACME Corp" with Docker socket "/var/run/docker.sock"
    Then the environment should exist with name "local"
    And the environment should have type "DockerSocket"
    And the environment should not be default

Scenario: Create default environment
    When I create default environment "primary" for "ACME Corp"
    Then the environment should exist with name "primary"
    And the connection config should use default Docker socket

Scenario: Set environment as default
    Given environment "staging" exists for "ACME Corp"
    When I set environment "staging" as default
    Then environment "staging" should be default

Scenario: Only one default environment per organization
    Given environment "production" exists for "ACME Corp" and is default
    And environment "staging" exists for "ACME Corp"
    When I set environment "staging" as default
    Then environment "staging" should be default
    And environment "production" should not be default

Scenario: Update environment name
    Given environment "old-name" exists for "ACME Corp"
    When I update environment name to "new-name"
    Then the environment should exist with name "new-name"

Scenario: Environment name is required
    When I try to create environment with empty name for "ACME Corp"
    Then the operation should fail with error "Environment name is required"

Scenario: Environment name length is limited
    When I try to create environment with name longer than 100 characters for "ACME Corp"
    Then the operation should fail with error "Environment name must be 100 characters or less"
