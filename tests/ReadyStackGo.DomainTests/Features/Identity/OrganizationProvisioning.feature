Feature: Organization Provisioning
    As an administrator
    I want to provision organizations
    So that users can manage their Docker stacks

Background:
    Given the system has no tenants configured

Scenario: Provision organization assigns OrganizationOwner role
    Given user "admin" exists with "SystemAdmin" role globally
    When user "admin" provisions organization "ACME Corp" with description "Main organization"
    Then the organization "ACME Corp" should exist and be active
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
