Feature: Safe fetching
  Orbs fetches web pages for people, so it must not become a way into the network it runs in.
  Addresses on private networks are refused unless an admin allows them.

  Scenario: A web page on the local network is refused
    When I add the web page "http://127.0.0.1:9/secret" as "Router"
    Then the source "Router" failed because "private networks"
