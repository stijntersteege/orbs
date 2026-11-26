Feature: Chatting with an orb
  People talk to an orb and see its reply appear as it is written.
  The reply comes from whatever model the orb uses.

  Background:
    Given an orb named "Pip"

  Scenario: An orb replies using the connected model
    Given the demo model is connected
    When I say "Hello there" to "Pip"
    Then "Pip" replies "You said: Hello there"
    And the conversation is titled "Hello there"
    And the conversation has 2 messages

  Scenario: An orb without a model asks for one
    When I say "Hello there" to "Pip"
    Then I am told to connect a model first

  Scenario: A failing model does not lose the question
    Given a model that fails mid-reply is connected
    When I say "Hello there" to "Pip"
    Then I see an error mentioning "provider exploded"
    And the conversation has 1 message
