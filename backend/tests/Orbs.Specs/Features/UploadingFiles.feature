Feature: Uploading files
  People give orbs documents and pictures: PDFs, Word and Markdown files, images.
  Documents are read into text; pictures go to models that can see.

  Scenario: An uploaded document is something an orb can read
    When I upload "groceries.md" saying "Oat milk, blueberries, rye bread"
    Then the source "groceries" is ready

  Scenario: An orb looks at an uploaded picture
    Given the demo model is connected
    And I upload the picture "whiteboard.png"
    And an orb named "Pip" that knows "whiteboard"
    When I say "What do you see?" to "Pip"
    Then "Pip" replies "You said: What do you see? (and showed me 1 picture)"
