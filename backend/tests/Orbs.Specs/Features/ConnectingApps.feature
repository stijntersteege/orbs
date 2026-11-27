Feature: Connecting apps
  Orbs come with starter tools for web search, a calendar, email and Home Assistant.
  Connect an app once and its tools are there for every orb.

  Scenario: Connecting Home Assistant gives orbs smart home tools
    When I connect "home-assistant" as "Home" with the address "http://homeassistant.local:8123"
    Then the tool "home_list_entities" is available
    And the tool "home_call_service" is available

  Scenario: Sending email is something an orb asks about first
    Given an orb named "Pip"
    And the demo model is connected
    And I connect an email account with sending
    When I say '/tool email_send {"to":"sam@example.com","subject":"Hi","body":"Hello"}' to "Pip"
    Then "Pip" asks to use "email_send"
