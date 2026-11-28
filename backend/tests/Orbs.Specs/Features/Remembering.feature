Feature: Remembering
  Orbs remember what matters across chats. People can see, edit and forget every memory.

  Background:
    Given an orb named "Pip"
    And the demo model is connected

  Scenario: An orb saves a fact during a chat
    When I say '/tool remember {"fact":"Sam drinks green tea"}' to "Pip"
    Then "Pip" remembers "Sam drinks green tea"

  Scenario: Forgetting a memory
    Given "Pip" remembers that "Sam lives in Utrecht"
    When I forget the memory "Sam lives in Utrecht"
    Then "Pip" remembers nothing
