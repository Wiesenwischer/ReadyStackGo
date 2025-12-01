Feature: User Authentication
    As a user
    I want to authenticate with my credentials
    So that I can access the system

Background:
    Given organization "ACME Corp" exists and is active
    And user "admin" exists with password "SecurePass123!"

Scenario: Successful authentication
    When I authenticate with username "admin" and password "SecurePass123!"
    Then authentication should succeed

Scenario: Authentication fails with wrong password
    When I authenticate with username "admin" and password "WrongPassword"
    Then authentication should fail

Scenario: Authentication fails for disabled user
    Given user "admin" is disabled
    When I authenticate with username "admin" and password "SecurePass123!"
    Then authentication should fail

Scenario: Authentication fails for inactive organization
    Given organization "ACME Corp" is deactivated
    When I authenticate with username "admin" and password "SecurePass123!"
    Then authentication should fail

Scenario: Authentication fails for non-existent user
    When I authenticate with username "nonexistent" and password "AnyPassword123!"
    Then authentication should fail
