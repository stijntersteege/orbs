using System.Text;
using Orbs.Application.Skills;

namespace Orbs.Api.Endpoints;

internal static class SkillEndpoints
{
    public static void MapSkillEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/skills").WithTags("Skills");

        group.MapGet("/", (SkillService skills, CancellationToken ct) => skills.ListAsync(ct))
            .WithSummary("Saved skills (routines) every orb can use");

        group.MapGet("/{id:guid}", (Guid id, SkillService skills, CancellationToken ct) => skills.GetAsync(id, ct))
            .WithSummary("One skill");

        group.MapPost("/", async (SaveSkillRequest request, SkillService skills, CancellationToken ct) =>
            {
                var skill = await skills.CreateAsync(request, ct);
                return TypedResults.Created($"/api/skills/{skill.Id}", skill);
            })
            .WithSummary("Save a new skill");

        group.MapPut("/{id:guid}", (Guid id, SaveSkillRequest request, SkillService skills, CancellationToken ct) => skills.UpdateAsync(id, request, ct))
            .WithSummary("Change a skill");

        group.MapDelete("/{id:guid}", async (Guid id, SkillService skills, CancellationToken ct) =>
            {
                await skills.DeleteAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithSummary("Delete a skill");

        group.MapPost("/import", (ImportSkillRequest request, SkillService skills, CancellationToken ct) => skills.ImportAsync(request, ct))
            .WithSummary("Add a skill from a SKILL.md file; one with the same name is replaced");

        group.MapGet("/{id:guid}/export", async (Guid id, SkillService skills, CancellationToken ct) =>
            {
                var file = await skills.ExportAsync(id, ct);
                return TypedResults.File(Encoding.UTF8.GetBytes(file.Markdown), "text/markdown; charset=utf-8", file.FileName);
            })
            .WithSummary("Download a skill as a SKILL.md file");

        api.MapPost("/conversations/{id:guid}/messages/{messageId:guid}/skill", async (Guid id, Guid messageId, SkillFromReplyRequest request, SkillService skills, CancellationToken ct) =>
            {
                var skill = await skills.FromReplyAsync(id, messageId, request, ct);
                return TypedResults.Created($"/api/skills/{skill.Id}", skill);
            })
            .WithTags("Skills")
            .WithSummary("Save the tools a reply used as a skill");
    }
}
