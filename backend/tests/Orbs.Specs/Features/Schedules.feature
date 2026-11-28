Feature: Schedules and triggers
  Orbs can work on their own: at set times, when another app calls them, or when something new arrives.

  Background:
    Given an orb named "Pip"
    And the demo model is connected

  Scenario: A weekly summary
    Given "Pip" summarizes my week every Monday at 9:00
    When a week goes by
    Then "Pip" has written in the "Weekly summary" chat

  Scenario: Another app wakes an orb with a webhook
    Given "Pip" listens for a webhook called "Doorbell"
    When the webhook is called with "Sam is at the door"
    Then the "Doorbell" chat mentions "Sam is at the door"
