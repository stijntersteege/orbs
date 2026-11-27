Feature: Connecting MCP servers
  Any Model Context Protocol server becomes a set of tools for orbs.
  Its tools follow the same permissions and approvals as built-in tools.

  Scenario: An MCP server's tools become available to orbs
    When I connect the MCP server "Home" at "https://mcp.example.com/mcp"
    Then the tool "home__shout" is available

  Scenario: An orb asks before using an MCP tool that acts
    Given an orb named "Pip"
    And the demo model is connected
    And I connect the MCP server "Home" at "https://mcp.example.com/mcp"
    When I say "/tool home__turn_on" to "Pip"
    Then "Pip" asks to use "home__turn_on"
