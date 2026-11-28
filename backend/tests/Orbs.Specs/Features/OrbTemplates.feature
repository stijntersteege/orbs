Feature: Orb templates
  People share an orb as one file: its face, brief, voice, tool permissions and schedules.

  Scenario: Sharing an orb with someone else
    Given an orb named "Chef"
    When I export "Chef" as a template
    And someone else imports that template
    Then they have an orb named "Chef"
