Feature: Notifications
  Orbs reach out when they did something on their own, but never during quiet hours.

  Background:
    Given an orb named "Pip"
    And the demo model is connected
    And notifications are on for this device
    And "Pip" listens for a webhook called "Parcel"

  Scenario: An orb tells me what it did
    When the webhook is called with "Your parcel arrived"
    Then I get a notification from "Pip"

  Scenario: Quiet hours hold notifications back
    Given it is quiet hours right now
    When the webhook is called with "Your parcel arrived"
    Then I get no notification yet
