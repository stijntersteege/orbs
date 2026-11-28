Feature: Files in a chat
  People drop a file into a chat. The orb reads it in that chat, and keeps it for later chats only when asked to.

  Background:
    Given the demo model is connected
    And an orb named "Pip"

  Scenario: A file for one chat only
    When I attach a file "menu.md" saying "Soup on Friday" to a chat with "Pip"
    And I say "What is for dinner?" to "Pip"
    Then the reply read the source "menu"
    And "Pip" does not keep "menu" for other chats
