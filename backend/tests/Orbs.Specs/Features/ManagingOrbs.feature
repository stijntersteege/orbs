Feature: Managing orbs
  An orb is a model, its context and a face.
  People create orbs, shape how they look and behave, and remove them when done.

  Scenario: Create an orb with a name and a purpose
    When I create an orb named "Pip" described as "Work sidekick"
    Then I have 1 orb
    And the orb "Pip" is described as "Work sidekick"
    And the orb "Pip" uses the default look

  Scenario: An orb needs a name
    When I try to create an orb named ""
    Then the request is refused because "Name is required."

  Scenario Outline: Give an orb its own face
    Given an orb named "Pip"
    When I give "Pip" the color "<color>" and the eyes "<eyes>"
    Then "Pip" has the color "<color>" and the eyes "<eyes>"

    Examples:
      | color     | eyes   |
      | mint      | visor  |
      | coral     | wink   |
      | bubblegum | sleepy |

  Scenario: Removing an orb
    Given an orb named "Pip"
    When I delete the orb "Pip"
    Then I have 0 orbs
