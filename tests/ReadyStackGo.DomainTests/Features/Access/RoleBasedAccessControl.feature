Feature: Role-Based Access Control
    As an administrator
    I want to assign roles to users
    So that they have appropriate permissions

Background:
    Given organization "ACME Corp" exists
    And environment "Production" exists in "ACME Corp"
    And user "admin" is SystemAdmin
    And user "operator" exists in "ACME Corp"
    And user "viewer" exists in "ACME Corp"

Scenario: SystemAdmin has all permissions globally
    Then user "admin" should have permission "Users.Create" with global scope
    And user "admin" should have permission "Users.Delete" with global scope
    And user "admin" should have permission "Deployments.Create" with global scope
    And user "admin" should have permission "Environments.Create" with global scope

Scenario: Operator can deploy stacks
    Given user "operator" has role "Operator" for environment "Production"
    Then user "operator" should have permission "Deployments.Create" for environment "Production"
    And user "operator" should have permission "Deployments.Delete" for environment "Production"

Scenario: Operator cannot manage users
    Given user "operator" has role "Operator" for environment "Production"
    Then user "operator" should not have permission "Users.Create" with global scope

Scenario: Viewer can only read
    Given user "viewer" has role "Viewer" for organization "ACME Corp"
    Then user "viewer" should have permission "Deployments.Read" for organization "ACME Corp"
    And user "viewer" should not have permission "Deployments.Create" for organization "ACME Corp"
    And user "viewer" should not have permission "Deployments.Delete" for organization "ACME Corp"

Scenario: Organization scope covers environments
    Given user "operator" has role "Operator" for organization "ACME Corp"
    And environment "Staging" exists in "ACME Corp"
    Then user "operator" should have permission "Deployments.Create" for environment "Production"
    And user "operator" should have permission "Deployments.Create" for environment "Staging"

Scenario: User can have multiple roles
    Given user "operator" has role "Operator" for environment "Production"
    And user "operator" has role "Viewer" for organization "ACME Corp"
    Then user "operator" should have permission "Deployments.Create" for environment "Production"
    And user "operator" should have permission "Deployments.Read" for organization "ACME Corp"
