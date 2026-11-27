Feature: Group chats
  People bring several orbs into one chat by mentioning them. Each mentioned orb answers in turn.

  Background:
    Given the demo model is connected
    And an orb named "Pip"
    And an orb named "Nova"

  Scenario: Handing a question to another orb
    When I say "@Nova can you help?" to "Pip"
    Then "Nova" answers in the chat
    And the chat has "Pip" and "Nova" in it
