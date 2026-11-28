Feature: Using tools
  Orbs can do more than talk: they can look things up and act through tools.
  Every tool call, what went in and what came out, is kept with the reply.

  Background:
    Given an orb named "Pip"
    And the demo model is connected

  Scenario: An orb uses a tool to answer
    When I say '/tool calculator {"expression":"2+3"}' to "Pip"
    Then "Pip" replies "calculator said: 5"
    And the reply used the tool "calculator" with the result "5"

  Scenario: The tools an orb used stay visible afterwards
    When I say "/tool current_time" to "Pip"
    Then the last saved reply lists the tool "current_time"
