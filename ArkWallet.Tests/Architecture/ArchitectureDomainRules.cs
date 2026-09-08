using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ArkWallet.Tests.Architecture;

public class ArchitectureDomainRules
{
    [Fact]
    public void Rule12_AggregateRootAccessesOnlyItsOwnParts()
    {
        var violations = new List<string>();
        foreach (var root in ArkKinds.RootsCollection)
        {
            var ownContext = ArkArchitecture.ContextOf(root);
            if (ownContext == null)
            {
                continue;
            }

            var ownParts = ArkKinds.PartsOf(root);
            var foreignPartsInContext = ArkKinds.PartsOf(ownContext)
                .Where(p => !ownParts.Contains(p));

            var denied = ArkKinds.NonDomainLayers()
                .Concat(ArkKinds.DomainExcept(ownContext, ArkArchitecture.GeneralContext))
                .Concat(foreignPartsInContext);

            foreach (var (source, target) in ArkKinds.ViolatingDependencies(new[] { root }, denied))
            {
                violations.Add($"{source} -> {target}");
            }
        }

        Assert.True(violations.Count == 0,
            $"Корень агрегата обращается только к своим частям и допустимым в контексте корням (правило 12).{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    [Fact]
    public void Rule13_AggregatePartAccessesOnlyItsOwnRoot()
    {
        var violations = new List<string>();
        foreach (var part in ArkKinds.PartsCollection)
        {
            var root = ArkKinds.RootOf(part);
            var ownContext = ArkArchitecture.ContextOf(part);
            if (root == null || ownContext == null)
            {
                continue;
            }

            var deniedInContext = ArkKinds.DomainOf(ownContext)
                .Where(d => d != root && d != part);

            var denied = ArkKinds.DomainExcept(ownContext, ArkArchitecture.GeneralContext)
                .Concat(deniedInContext);

            foreach (var (source, target) in ArkKinds.ViolatingDependencies(new[] { part }, denied))
            {
                violations.Add($"{source} -> {target}");
            }
        }

        Assert.True(violations.Count == 0,
            $"Часть агрегата обращается только к своему корню (правило 13).{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    [Fact]
    public void Rule14_OnlyAggregateRootAccessesItsParts()
    {
        var violations = new List<string>();
        foreach (var part in ArkKinds.PartsCollection)
        {
            var root = ArkKinds.RootOf(part);
            if (root == null)
            {
                continue;
            }

            var accessors = part.BackwardsDependencies
                .Select(d => d.Origin)
                .Where(o => o != null
                            && o.FullName.StartsWith("ArkWallet.", StringComparison.Ordinal)
                            && !o.IsCompilerGenerated
                            && o.FullName != part.FullName
                            && o.FullName != root.FullName)
                .Select(o => o.FullName)
                .Distinct()
                .ToArray();

            foreach (var accessor in accessors)
            {
                violations.Add($"{accessor} -> {part.FullName}");
            }
        }

        Assert.True(violations.Count == 0,
            $"К частям агрегата обращается только корень агрегата (правило 14).{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    [Fact]
    public void Rule15_DomainEventsArePublishedOnlyAtDomainLevel()
    {
        var corePublisherTypes = new List<IType>();
        if (ArkArchitecture.FindType(ArkNamespaces.IEventPublisherFullName) is { } eventPublisher)
        {
            corePublisherTypes.Add(eventPublisher);
        }

        if (ArkArchitecture.FindType(ArkNamespaces.AggregateRootFullName) is { } aggregateRoot)
        {
            corePublisherTypes.Add(aggregateRoot);
        }

        var mediatrTypes = new List<IType>();
        if (ArkArchitecture.FindType(ArkNamespaces.MediatrIPublisherFullName) is { } ipublisher)
        {
            mediatrTypes.Add(ipublisher);
        }

        var publishCalls = MethodMembers()
            .That()
            .AreDeclaredIn(ArkArchitecture.Of(corePublisherTypes))
            .And()
            .HaveName("PublishAsync")
            .Or()
            .AreDeclaredIn(ArkArchitecture.Of(mediatrTypes))
            .And()
            .HaveName("Publish");

        Classes().That().AreNot(ArkArchitecture.Of(ArkKinds.DomainTypesAll()))
            .Should()
            .NotCallAny(publishCalls)
            .Because("DomainEvents публикуются (вызываются) только на уровне Domain, не более низкими слоями (правило 15).")
            .Check(ArkArchitecture.Model);
    }
}