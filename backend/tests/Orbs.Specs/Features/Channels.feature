Feature: Channels
  An orb can live in a messaging app as its own bot. Only chats paired with a one-time code get answers.

  Background:
    Given an orb named "Pip"
    And the demo model is connected
    And "Pip" is connected to the test chat app

  Scenario: Pairing a chat and talking to the orb
    When "Sam" sends the pairing code
    And "Sam" sends "Good morning"
    Then "Sam" gets the answer "You said: Good morning"

  Scenario: Someone without the code gets nothing
    When "Eve" sends "Hello?"
    Then "Eve" gets no answer
