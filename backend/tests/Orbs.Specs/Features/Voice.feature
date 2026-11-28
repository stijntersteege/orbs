Feature: Voice
  People can talk to their orbs and hear them answer, with any provider that has speech models.

  Scenario: Speaking to an orb and hearing the answer
    Given a provider that can listen and speak
    When I say out loud "Good morning"
    Then Orbs hears "Good morning"
    And Orbs can read the answer aloud

  Scenario: Each orb has its own voice
    Given a provider that can listen and speak
    And an orb named "Pip" with the voice "nova"
    Then "Pip" reads its answers in the voice "nova"
