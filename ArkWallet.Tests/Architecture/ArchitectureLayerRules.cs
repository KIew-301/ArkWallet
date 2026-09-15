using ArchUnitNET.Domain;
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

    [Fact]
    public void Rule18_DataEntitiesDoNotHaveAnyMethods()
    {
        const string dbContextFullName = "ArkWallet.Infrastructure.Data.ArkWalletDbContext";
        const string entityDataFullName = "ArkWallet.Infrastructure.Data.EntityData";

        var dataClasses = ArkArchitecture.Model.Classes
            .Where(c => !c.IsCompilerGenerated
                        && c.IsRecord != true
                        && ArkKinds.IsData(c)
                        && c.FullName != dbContextFullName
                        && c.FullName != entityDataFullName)
            .ToArray();

        var inheritanceViolations = dataClasses
            .Where(c => !c.InheritedClasses.Select(b => b.FullName).Contains(entityDataFullName))
            .Select(c => $"{c.FullName} (не наследует {entityDataFullName})")
            .ToArray();

        var methodViolations = dataClasses
            .SelectMany(c => c.Members
                .OfType<MethodMember>()
                .Where(m => !m.IsCompilerGenerated && !IsAllowedDataOperation(ShortMethodName(m.Name)))
                .Select(m => $"{c.FullName}.{m.Name}"))
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToArray();

        var violations = inheritanceViolations.Concat(methodViolations)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToArray();

        Assert.True(violations.Length == 0,
            $"Сущности Data наследуют {entityDataFullName} и содержат только операции создания, обновления и копирования (правило 18).{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    private static bool IsAllowedDataOperation(string methodName)
    {
        return methodName.StartsWith(".ctor", StringComparison.Ordinal)
               || methodName.StartsWith(".cctor", StringComparison.Ordinal)
               || methodName.StartsWith("get_", StringComparison.Ordinal)
               || methodName.StartsWith("set_", StringComparison.Ordinal)
               || methodName is "Create" or "Update" or "Copy";
    }

    private static string ShortMethodName(string methodName)
    {
        var paren = methodName.IndexOf('(');
        return paren >= 0 ? methodName[..paren] : methodName;
    }
}