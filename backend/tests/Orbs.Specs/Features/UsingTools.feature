Feature: Using tools
  Orbs can do more than talk: they can look things up and act through tools.
  By default an orb asks before every tool call; people can allow or block tools per orb.
  Every tool call, what went in and what came out, is kept with the reply.

  Background:
    Given an orb named "Pip"
    And the demo model is connected

  Scenario: An allowed tool runs without asking
    Given "Pip" may use "calculator" without asking
    When I say '/tool calculator {"expression":"2+3"}' to "Pip"
    Then "Pip" replies "calculator said: 5"
    And the reply used the tool "calculator" with the result "5"

  Scenario: An orb asks first, and runs the tool once I approve
    When I say "/tool current_time" to "Pip"
    Then "Pip" asks to use "current_time"
    When I approve the tool call
    Then the reply finishes and the last saved reply lists the tool "current_time"

  Scenario: Declining a tool call
    When I say '/tool calculator {"expression":"1+1"}' to "Pip"
    And I decline the tool call
    Then "Pip" replies without having run "calculator"

  Scenario: A blocked tool is not offered at all
    Given "Pip" may never use "current_time"
    When I say "/tool current_time" to "Pip"
    Then "Pip" replies "You said: /tool current_time"
