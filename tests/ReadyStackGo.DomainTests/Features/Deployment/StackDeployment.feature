Feature: Stack Deployment
    As an operator
    I want to deploy stacks to environments
    So that I can run Docker services

Background:
    Given organization "ACME Corp" exists
    And environment "production" exists for "ACME Corp"
    And user "operator" exists with "Operator" role for "ACME Corp"

Scenario: Start a new deployment
    When I start deployment "my-stack" to environment "production" as "operator"
    Then the deployment should have status "Pending"
    And the deployment should have phase "Initializing"
    And the deployment should have progress 0%

Scenario: Track deployment progress
    Given deployment "my-stack" is started to environment "production"
    When I update progress to phase "PullingImages" at 50% with message "Pulling 3 images"
    Then the deployment should have phase "PullingImages"
    And the deployment should have progress 50%

Scenario: Complete deployment successfully
    Given deployment "my-stack" is started to environment "production"
    When I mark the deployment as running with services:
        | ServiceName | ContainerId  | Status  |
        | api         | abc123       | running |
        | database    | def456       | running |
    Then the deployment should have status "Running"
    And the deployment should have 2 services
    And all services should be healthy

Scenario: Fail deployment with error
    Given deployment "my-stack" is started to environment "production"
    When I mark the deployment as failed with error "Image pull failed: nginx:latest"
    Then the deployment should have status "Failed"
    And the deployment should have phase "Failed"
    And the error message should contain "Image pull failed"

Scenario: Stop a running deployment
    Given deployment "my-stack" is running with services
    When I stop the deployment
    Then the deployment should have status "Stopped"

Scenario: Restart a stopped deployment
    Given deployment "my-stack" is stopped
    When I restart the deployment
    Then the deployment should have status "Running"
    And the deployment should have phase "Starting"

Scenario: Cannot restart a failed deployment
    Given deployment "my-stack" has failed
    When I try to restart the deployment
    Then the operation should fail with error "Invalid state transition"

Scenario: Cancel a pending deployment
    Given deployment "my-stack" is pending
    When I request cancellation with reason "Wrong environment selected"
    Then cancellation should be requested
    And the cancellation reason should be "Wrong environment selected"

Scenario: Valid state transitions
    Given deployment "my-stack" has status "Pending"
    Then the valid next states should be "Running, Failed"
    When the deployment status changes to "Running"
    Then the valid next states should be "Stopped, Failed"

Scenario: Remove a stopped deployment
    Given deployment "my-stack" is stopped
    When I remove the deployment
    Then the deployment should have status "Removed"
    And the deployment should be terminal
