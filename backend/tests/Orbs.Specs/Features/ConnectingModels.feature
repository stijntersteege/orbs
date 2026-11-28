Feature: Connecting models
  People connect the model providers they like. Besides any OpenAI-compatible endpoint,
  Anthropic and Google Gemini have native support, including tools.

  Scenario: Anthropic and Gemini can be connected with just a key
    Then I can connect a "anthropic" provider without a base URL
    And I can connect a "gemini" provider without a base URL
