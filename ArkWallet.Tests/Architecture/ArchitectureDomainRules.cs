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
                .Where(d => d != root && d != part
                    && ArkKinds.DomainFolder(d) != "Events"
                    && !d.Name.EndsWith("Data", StringComparison.Ordinal)
                    && !d.Name.EndsWith("Dto", StringComparison.Ordinal)
                    && !d.Name.EndsWith("Command", StringComparison.Ordinal));

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
                .Where(o =>
                {
                    if (o.Name.EndsWith("Mapper", StringComparison.Ordinal)
                        || o.Name.EndsWith("Data", StringComparison.Ordinal)
                        || o.Name.EndsWith("Dto", StringComparison.Ordinal)
                        || o.Name.EndsWith("EventHandler", StringComparison.Ordinal))
                    {
                        return false;
                    }

                    if (o is Class cls && ArkKinds.IsDomain(cls))
                    {
                        var folder = ArkKinds.DomainFolder(cls);
                        if (folder is "Events" or "Engines")
                        {
                            return false;
                        }
                    }

                    return true;
                })
                .Select(o => o.FullName!)
                .Distinct()
                .ToArray();

            foreach (var accessor in accessors)
            {
                violations.Add($"{accessor} -> {part.FullName}");
            }
        }

        Assert.True(violations.Count == 0,
            $"К частям агрегата обращается только корень агрегата, мапперы и Domain Events (правило 14).{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    [Fact]
    public void Rule16_DomainEventsAreDefinedOnlyInDomainEventsFolder()
    {
        var violations = ArkArchitecture.Model.Classes
            .Where(c => !c.IsCompilerGenerated
                        && c.ImplementedInterfaces.Any(i => i.Name == "INotification`1"))
            .Where(c => !ArkKinds.IsDomain(c) || ArkKinds.DomainFolder(c) != "Events")
            .Select(c => c.FullName)
            .ToArray();

        Assert.True(violations.Length == 0,
            $"Domain Events (INotification) должны располагаться в Domain/Events каждого контекста (правило 16).{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    [Fact]
    public void Rule17_DomainEventsDependOnlyOnDomainTypes()
    {
        var violations = new List<string>();
        var domainEvents = ArkArchitecture.Model.Classes
            .Where(c => !c.IsCompilerGenerated
                        && ArkKinds.IsDomain(c)
                        && ArkKinds.DomainFolder(c) == "Events")
            .ToArray();

        foreach (var evt in domainEvents)
        {
            foreach (var dep in evt.Dependencies)
            {
                var target = dep.Target?.FullName;
                if (target == null || target == evt.FullName)
                {
                    continue;
                }

                if (target.StartsWith("ArkWallet.Core.", StringComparison.Ordinal))
                {
                    if (ArkArchitecture.InNamespace(dep.Target!, ArkNamespaces.DomainPattern))
                    {
                        continue;
                    }

                    violations.Add($"{evt.FullName} -> {target}");
                    continue;
                }

                if (target.StartsWith("MediatR.", StringComparison.Ordinal)
                    || target.StartsWith("System.", StringComparison.Ordinal))
                {
                    continue;
                }

                violations.Add($"{evt.FullName} -> {target}");
            }
        }

        Assert.True(violations.Count == 0,
            $"Domain Events ссылаются только на Domain-типы (правило 17).{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
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