using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ArkWallet.Tests.Architecture;

public class ArchitectureServiceRules
{
    [Fact]
    public void Rule02_ServicesAccessOnlyAggregateRoots_NotParts()
    {
        var violations = ArkKinds.ViolatingDependencies(ArkKinds.ServiceClasses, ArkKinds.PartsCollection)
            .Select(v => $"{v.Source} -> {v.Target}")
            .ToArray();

        Assert.True(violations.Length == 0,
            $"Application-сервисы обращаются только к корням агрегатов, не к частям (правило 2).{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    [Fact]
    public void Rule03_ServicesAccessRootsOnlyOfOwnContextOrGeneral()
    {
        foreach (var context in ArkArchitecture.Contexts)
        {
            var services = ArkKinds.ServiceClassesOf(context);
            if (services.Count == 0)
            {
                continue;
            }

            var ownAndGeneralRoots = ArkKinds.RootsOf(context)
                .Concat(ArkKinds.RootsOf(ArkArchitecture.GeneralContext))
                .ToArray();

            var deniedRoots = ArkKinds.RootsCollection
                .Where(r => !ownAndGeneralRoots.Contains(r))
                .ToArray();

            Classes().That().Are(ArkArchitecture.Of(services))
                .Should()
                .NotDependOnAny(ArkArchitecture.Of(deniedRoots))
                .Because($"Сервисы контекста {context} обращаются к корням только своего контекста или General (правило 3).")
                .Check(ArkArchitecture.Model);
        }
    }

    [Fact]
    public void Rule06_HandlersCallServicesOnlyOfOwnContext()
    {
        foreach (var context in ArkArchitecture.Contexts)
        {
            var handlers = ArkKinds.HandlersOf(context);
            if (handlers.Count == 0)
            {
                continue;
            }

            var deniedServices = ArkKinds.ServiceClasses
                .Where(s => ArkArchitecture.ContextOf(s) != context)
                .ToArray();

            Classes().That().Are(ArkArchitecture.Of(handlers))
                .Should()
                .NotDependOnAny(ArkArchitecture.Of(deniedServices))
                .Because($"Handlers контекста {context} слушают события других контекстов, но вызывают сервисы только своего (правило 6).")
                .Check(ArkArchitecture.Model);
        }
    }

    [Fact]
    public void Rule07_DbToDomainMappingGoesThroughDedicatedMapper()
    {
        var violations = ArkKinds.ServicesCollection
            .Where(s => ArkKinds.DependsOn(s, ArkKinds.DataTypes().OfType<Class>().ToArray())
                       && ArkKinds.DependsOn(s, ArkKinds.DomainClassesCollection)
                       && !ArkKinds.DependsOn(s, ArkKinds.MappersCollection))
            .Select(s => s.FullName)
            .ToArray();

        Assert.True(violations.Length == 0,
            $"Сервисы, работающие и с базой, и с Domain, обязаны пользоваться dedicated-маппером (правило 7).{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    [Fact]
    public void Rule10_ServiceImplementationContextMatchesItsContractContext()
    {
        var contractInterfaces = ArkKinds.ContractInterfacesCollection
            .Where(i => i.Name.StartsWith('I')
                       && (i.Name.EndsWith("Service", StringComparison.Ordinal)
                           || i.Name.EndsWith("Orchestrator", StringComparison.Ordinal)))
            .ToArray();

        var mismatches = new List<string>();
        foreach (var iface in contractInterfaces)
        {
            var contractContext = ArkArchitecture.ContextOf(iface);
            if (contractContext == null)
            {
                continue;
            }

            var implementations = ArkArchitecture.Model.Classes
                .Where(c => c.Name == iface.Name[1..] && !c.IsNested)
                .Select(c => (Class: c, Context: ArkArchitecture.ContextOf(c)))
                .Where(p => p.Context != null)
                .ToArray();

            if (implementations.Length == 0)
            {
                continue;
            }

            if (!implementations.Any(p => p.Context == contractContext))
            {
                mismatches.AddRange(implementations
                    .Select(p => $"{iface.FullName} -> {p.Class.FullName}")
                    .Distinct());
            }
        }

        Assert.True(mismatches.Count == 0,
            $"Контекст реализации сервиса должен совпадать с контекстом его интерфейса (правило 10).{Environment.NewLine}{string.Join(Environment.NewLine, mismatches)}");
    }

    [Fact]
    public void Rule11_ServiceDoesNotCallAnotherServiceOrOrchestrator()
    {
        var violations = ArkKinds.ViolatingDependencies(ArkKinds.ServicesCollection, ArkKinds.ServiceClasses)
            .Select(v => $"{v.Source} -> {v.Target}")
            .ToArray();

        Assert.True(violations.Length == 0,
            $"Сервис не вызывает другой сервис или оркестратор — только агрегат, маппер, DbContext и статические функции (правило 11).{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    [SkippableFact]
    public void Rule05_ServiceLogicIsLimitedToValidation()
    {
        Skip.If(true,
            "Правило 5 (в сервисах только валидация существования сущностей, строк и параметров) не проверяется статически. Требует ручной ревизии бизнес-логики сервисов.");
    }

    [SkippableFact]
    public void Rule08_ClassesArePlacedIntoTheirOwningContext()
    {
        Skip.If(true,
            "Правило 8 (класс, работающий только с контекстом X, лежит в X) частично покрывается правилами 1, 3, 6, 10, 11 и требует ручной ревизии оставшихся пересечений.");
    }
}