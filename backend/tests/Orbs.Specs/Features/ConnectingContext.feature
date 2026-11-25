Feature: Connecting context
  Context is everything orbs can know. A source is connected once,
  then shared only with the orbs people pick.

  Scenario: A note is ready to use straight away
    When I add a note "Team" saying "Sam leads design."
    Then the source "Team" is ready

  Scenario: An orb only sees the sources it is given
    Given a note "Team" saying "Sam leads design."
    And a note "Recipes" saying "Frittata takes 25 minutes."
    When I create an orb named "Pip" that knows "Team"
    Then "Pip" knows "Team"
    And "Pip" does not know "Recipes"

  Scenario: Deleting a source removes it from every orb
    Given a note "Team" saying "Sam leads design."
    And an orb named "Pip" that knows "Team"
    When I delete the source "Team"
    Then "Pip" knows nothing
