Feature: Accounts
  Everyone who uses an Orbs server has their own account.
  Orbs, models, context and chats belong to one account; nobody else sees them.

  Scenario: The first account receives what was there before
    Given a server with the demo data and no accounts
    When I create the first account
    Then I am an admin
    And I have 1 orb

  Scenario: People do not see each other's orbs
    Given an orb named "Pip"
    And another account "sam@orbs.test"
    Then "sam@orbs.test" has no orbs
    And I have 1 orb

  Scenario: Nobody gets in without signing in
    Then a visitor who is not signed in is refused
