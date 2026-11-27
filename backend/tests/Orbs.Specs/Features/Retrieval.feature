Feature: Retrieval
  Big sources do not fit in a prompt. With an embedding model, Orbs splits each source into passages
  and sends only the passages that match the question.

  Scenario: An orb reads the matching passages of a long source
    Given the demo model is connected with embeddings
    And a long note "Manual" that mentions "the router password is on the sticker" once
    And an orb named "Pip" that knows "Manual"
    When the background jobs run
    And I say "Where is the router password?" to "Pip"
    Then the reply read passages from "Manual"
