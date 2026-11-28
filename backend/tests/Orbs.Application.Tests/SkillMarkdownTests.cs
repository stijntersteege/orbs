using Orbs.Application.Skills;
using Orbs.Domain.Common;

namespace Orbs.Application.Tests;

public class SkillMarkdownTests
{
    [Fact]
    public void A_skill_file_is_read_from_its_front_matter_and_body()
    {
        var parsed = SkillMarkdown.Parse("""
            ---
            name: weekly-review
            description: "Sum up the week: what happened, what's next"
            license: MIT
            ---

            # Weekly review

            1. Read the calendar.
            """);

        parsed.Name.ShouldBe("weekly-review");
        parsed.Description.ShouldBe("Sum up the week: what happened, what's next");
        parsed.Instructions.ShouldBe("# Weekly review\n\n1. Read the calendar.");
    }

    [Fact]
    public void Folded_descriptions_and_windows_line_endings_work()
    {
        var parsed = SkillMarkdown.Parse("---\r\nname: Trip Planner\r\ndescription: >\r\n  Plans trips\r\n  step by step\r\n---\r\nDo it.");

        parsed.Description.ShouldBe("Plans trips step by step");
        SkillMarkdown.ToName(parsed.Name).ShouldBe("trip-planner");
    }

    [Theory]
    [InlineData("no front matter")]
    [InlineData("---\nname: x\n---\nbody")]
    public void Files_without_a_name_and_description_are_refused(string text)
    {
        Should.Throw<DomainException>(() => SkillMarkdown.Parse(text));
    }

    [Fact]
    public void Rendering_and_reading_back_keeps_the_skill()
    {
        var text = SkillMarkdown.Render("quotes", "Says \"hi\" and \\ more", "Step one.");

        var parsed = SkillMarkdown.Parse(text);

        parsed.ShouldBe(new SkillMarkdown.Parsed("quotes", "Says \"hi\" and \\ more", "Step one."));
    }
}
