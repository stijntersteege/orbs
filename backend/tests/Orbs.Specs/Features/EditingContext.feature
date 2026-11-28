Feature: Viewing and editing context
  People see what a source holds, as their orbs read it, and edit notes and text files in place.

  Scenario: Editing a Markdown file
    When I upload "plan.md" saying "Ship on Friday"
    And I change "plan" to "Ship on Monday"
    Then "plan" holds "Ship on Monday"
