Feature: Managing chats
  People rename chats, delete single messages and delete whole chats.

  Background:
    Given the demo model is connected
    And an orb named "Pip"

  Scenario: Renaming a chat and deleting a reply
    When I say "Plan my week" to "Pip"
    And I rename the conversation to "Week plan"
    And I delete the last reply
    Then the conversation is titled "Week plan"
    And the conversation has 1 message
