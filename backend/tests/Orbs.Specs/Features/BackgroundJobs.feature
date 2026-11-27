Feature: Background jobs
  Some work happens while nobody is looking: schedules, syncs and long tasks.
  People can see what ran for them and how it went.

  Scenario: Work done in the background shows up in my list
    Given a background job for me that says "hello"
    When the background jobs run
    Then my latest background job succeeded with "hello"

  Scenario: Other people's background work stays private
    Given another account "sam@orbs.test"
    And a background job for "sam@orbs.test" that says "secret"
    When the background jobs run
    Then I have no background jobs
