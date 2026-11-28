Feature: Skills
  People save routines as skills. Every orb can read a skill and follow its steps.
  Skills use the SKILL.md format, so they move between Orbs and other agents.

  Background:
    Given the demo model is connected
    And an orb named "Pip"
    And "Pip" may use "calculator" without asking

  Scenario: Saving a reply's steps as a skill and using it later
    When I say '/tool calculator {"expression":"6*7"}' to "Pip"
    And I save that reply as the skill "Do the math"
    And I say '/tool use_skill {"skill":"do-the-math"}' to "Pip"
    Then the reply used the tool "use_skill" with a result mentioning "Use the `calculator` tool"
