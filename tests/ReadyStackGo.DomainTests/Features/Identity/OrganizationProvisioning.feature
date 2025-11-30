Feature: Organization Provisioning
    As a system installer
    I want to provision an organization with an admin user
    So that users can start managing their Docker stacks

Background:
    Given the system has no tenants configured

Scenario: Provision initial organization
    When I provision organization "ACME Corp" with description "Main organization"
    And I create admin user "admin" with email "admin@acme.com" and password "SecurePass123!"
    Then the organization "ACME Corp" should exist and be active
    And user "admin" should exist with email "admin@acme.com"
    And user "admin" should have role "SystemAdmin" with global scope
    And user "admin" should have role "OrganizationOwner" for organization "ACME Corp"

Scenario: Cannot provision organization with weak password
    When I try to provision organization "ACME Corp"
    And I try to create admin user "admin" with password "weak"
    Then the provisioning should fail with error "Password must be at least 8 characters"

Scenario: Cannot provision duplicate organization name
    Given organization "ACME Corp" exists
    When I try to provision organization "ACME Corp"
    Then the provisioning should fail with error "Organization name already exists"

Scenario: Organization name is required
    When I try to provision organization with empty name
    Then the provisioning should fail with error "Organization name is required"

Scenario: Admin email must be valid format
    When I try to provision organization "ACME Corp"
    And I try to create admin user "admin" with email "invalid-email" and password "SecurePass123!"
    Then the provisioning should fail with error "Email format is invalid"
