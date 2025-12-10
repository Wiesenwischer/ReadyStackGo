Feature: Initial Setup Wizard
    As a system installer
    I want to complete the initial setup wizard
    So that the system is ready for use

Background:
    Given the system is not initialized

Scenario: Complete wizard step 1 creates SystemAdmin
    When I complete wizard step 1 with user "admin" email "admin@acme.com" and password "SecurePass123!"
    Then user "admin" should exist with email "admin@acme.com"
    And user "admin" should have role "SystemAdmin" with global scope

Scenario: Complete wizard step 2 creates organization
    Given user "admin" completed wizard step 1 and is SystemAdmin
    When I complete wizard step 2 with organization "ACME Corp" and environment "production"
    Then the organization "ACME Corp" should exist and be active
    And user "admin" should have role "OrganizationOwner" for organization "ACME Corp"
    And environment "production" should exist for "ACME Corp"
    And environment "production" should be default

Scenario: Wizard cannot be completed twice
    Given the system is already initialized
    When I try to complete wizard step 1
    Then the operation should fail with error "System is already initialized"
