using System.Reflection;
using NetArchTest.Rules;

namespace Orbs.Architecture.Tests;

/// <summary>
/// Guards the clean-architecture dependency rule: source code points inwards only.
/// Api -> Infrastructure -> Application -> Domain.
/// </summary>
public class LayeringTests
{
    private static readonly Assembly Domain = typeof(Domain.Orbs.Orb).Assembly;
    private static readonly Assembly Application = typeof(Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void The_domain_depends_on_nothing_else()
    {
        var result = Types.InAssembly(Domain).ShouldNot()
            .HaveDependencyOnAny("Orbs.Application", "Orbs.Infrastructure", "Orbs.Api", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "System.Net.Http")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Failing(result));
    }

    [Fact]
    public void The_application_does_not_know_about_infrastructure_or_http()
    {
        var result = Types.InAssembly(Application).ShouldNot()
            .HaveDependencyOnAny("Orbs.Infrastructure", "Orbs.Api", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Failing(result));
    }

    [Fact]
    public void Infrastructure_does_not_know_about_the_api()
    {
        var result = Types.InAssembly(Infrastructure).ShouldNot().HaveDependencyOn("Orbs.Api").GetResult();

        result.IsSuccessful.ShouldBeTrue(Failing(result));
    }

    [Fact]
    public void Adapters_are_internal_so_only_their_ports_are_used()
    {
        var result = Types.InAssembly(Infrastructure)
            .That().ImplementInterface(typeof(Application.Abstractions.IModelAdapter))
            .Or().ImplementInterface(typeof(Application.Abstractions.IContextConnector))
            .Should().NotBePublic()
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Failing(result));
    }

    [Fact]
    public void Domain_entities_are_sealed()
    {
        var result = Types.InAssembly(Domain)
            .That().Inherit(typeof(Domain.Common.Entity))
            .And().AreNotAbstract()
            .Should().BeSealed()
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Failing(result));
    }

    private static string Failing(TestResult result) =>
        "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
