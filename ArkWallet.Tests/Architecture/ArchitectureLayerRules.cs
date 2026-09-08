using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ArkWallet.Tests.Architecture;

public class ArchitectureLayerRules
{
    [Fact]
    public void Rule01_DomainRelationsAreLimitedToOwnContextAndGeneral()
    {
        var violations = new List<string>();
        foreach (var context in ArkArchitecture.Contexts)
        {
            var denied = ArkKinds.NonDomainLayers().Concat(ArkKinds.DomainExcept(context, ArkArchitecture.GeneralContext));

            foreach (var (source, target) in ArkKinds.ViolatingDependencies(ArkKinds.DomainOf(context), denied))
            {
                violations.Add($"{source} -> {target}");
            }
        }

        Assert.True(violations.Count == 0,
            $"Domain-классы каждого контекста обращаются только к Domain того же контекста или General (правило 1).{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    [Fact]
    public void Rule04_ServicesDoNotReachIntoInfrastructureOrPresentation()
    {
        var denied = ArkKinds.InfrastructureExceptData().Concat(ArkKinds.PresentationTypes());

        Classes().That().Are(ArkArchitecture.Of(ArkKinds.ServiceClasses))
            .Should()
            .NotDependOnAny(ArkArchitecture.Of(denied))
            .Because("Сервисы зависят только от Domain, Application, DbContext и статических функций; Infrastructure — только через интерфейсы Application (правило 4).")
            .Check(ArkArchitecture.Model);
    }

    [Fact]
    public void Rule09_RootsPartsAndEnginesDoNotLiveInGeneral()
    {
        Classes().That().Are(ArkArchitecture.Of(ArkKinds.RootsAnalyzable))
            .Should()
            .NotBe(ArkArchitecture.Of(ArkKinds.GeneralDomainTypes()))
            .Because("Корни агрегатов, части и двигатели не могут располагаться в General-контексте (правило 9).")
            .Check(ArkArchitecture.Model);
    }
}