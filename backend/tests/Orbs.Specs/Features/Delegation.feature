Feature: Coordinator orbs
  An orb can hand a question to one of the person's other orbs, a specialist, and use its answer.

  Background:
    Given the demo model is connected
    And an orb named "Lead"
    And an orb named "Chef"

  Scenario: Asking a specialist
    When I say '/tool ask_orb {"orb":"Chef","question":"Dinner for four?"}' to "Lead"
    Then the reply used the tool "ask_orb" with the result "Chef says: You said: Dinner for four?"
