using System.Text.RegularExpressions;
using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;

namespace ArkWallet.Tests.Architecture;

public static class ArkNamespaces
{
    public const string DomainPattern = @"^ArkWallet\.Core\..*\.Domain(\.|$)";
    public const string ApplicationPattern = @"^ArkWallet\.Core\..*\.Application(\.|$)";
    public const string InfrastructurePattern = @"^ArkWallet\.(Infrastructure|Entities|Migrations)(\.|$)";
    public const string DataPattern = @"^ArkWallet\.Infrastructure\.Data(\.|$)";
    public const string PresentationPattern = @"^ArkWallet\.(Presentation|Telegram)(\.|$)";

    public const string AggregateRootFullName = "ArkWallet.Core.General.Domain.Common.AggregateRoot";
    public const string IEventPublisherFullName = "ArkWallet.Core.General.Domain.Common.IEventPublisher";
    public const string MediatrIPublisherFullName = "MediatR.IPublisher";
}

public static partial class ArkArchitecture
{
    private static readonly Lazy<ArchUnitNET.Domain.Architecture> LazyModel = new(() => new ArchLoader()
        .LoadAssemblies(typeof(Program).Assembly)
        .Build());

    public static ArchUnitNET.Domain.Architecture Model => LazyModel.Value;

    public const string GeneralContext = "General";

    private static string? CoreSegment(string namespaceName)
    {
        const string prefix = "ArkWallet.Core.";
        if (!namespaceName.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        return namespaceName[prefix.Length..].Split('.')[0];
    }

    private static readonly string[] DerivedBusinessContexts = Model.Namespaces
        .Where(ns => DomainPatternRegex().IsMatch(ns.FullName)
                     || ApplicationPatternRegex().IsMatch(ns.FullName))
        .Select(ns => CoreSegment(ns.FullName)!)
        .Where(segment => segment != null && segment != GeneralContext)
        .Distinct()
        .OrderBy(segment => segment, StringComparer.Ordinal)
        .ToArray();

    [GeneratedRegex(ArkNamespaces.DomainPattern)]
    private static partial Regex DomainPatternRegex();

    [GeneratedRegex(ArkNamespaces.ApplicationPattern)]
    private static partial Regex ApplicationPatternRegex();

    public static string[] BusinessContexts => DerivedBusinessContexts;

    public static readonly string[] Contexts = new[] { GeneralContext }.Concat(DerivedBusinessContexts).ToArray();

    public static bool InNamespace(IType type, string pattern) =>
        type.Namespace != null && Regex.IsMatch(type.Namespace.FullName, pattern);

    public static string? ContextOf(IType type)
    {
        var ns = type.Namespace?.FullName;
        if (ns == null || !ns.StartsWith("ArkWallet.Core.", StringComparison.Ordinal))
        {
            return null;
        }

        var segment = ns["ArkWallet.Core.".Length..].Split('.')[0];
        return Contexts.Contains(segment) ? segment : null;
    }

    public static IType? FindType(string fullName) =>
        Model.Types.FirstOrDefault(t => t.FullName == fullName)
        ?? Model.ReferencedTypes.FirstOrDefault(t => t.FullName == fullName);

    public static IObjectProvider<IType> Of(IEnumerable<IType> types, string? description = null)
    {
        var materialized = types.Distinct().ToArray();
        var key = string.IsNullOrEmpty(description)
            ? $"[{materialized.Length}]" + string.Join("|", materialized.Select(t => t.FullName ?? "").OrderBy(n => n, StringComparer.Ordinal).Take(5))
            : description;
        return new BasicObjectProvider<IType>(_ => materialized, key);
    }
}

public static class ArkKinds
{
    private static Lazy<T> LazyOf<T>(Func<T> factory) => new(factory);

    private static readonly HashSet<string> ExcludedFolders = new() { "Common", "Exceptions", "ValueObjects" };

    public static bool IsDomain(IType type) =>
        !type.IsCompilerGenerated && ArkArchitecture.InNamespace(type, ArkNamespaces.DomainPattern);

    public static bool IsApplication(IType type) =>
        !type.IsCompilerGenerated && ArkArchitecture.InNamespace(type, ArkNamespaces.ApplicationPattern);

    public static bool IsInfrastructure(IType type) =>
        ArkArchitecture.InNamespace(type, ArkNamespaces.InfrastructurePattern);

    public static bool IsData(IType type) =>
        ArkArchitecture.InNamespace(type, ArkNamespaces.DataPattern);

    public static bool IsPresentation(IType type) =>
        ArkArchitecture.InNamespace(type, ArkNamespaces.PresentationPattern);

    public static bool IsImplementationKind(IType type) =>
        !type.IsCompilerGenerated && !type.IsNested;

    private static readonly Lazy<IType[]> AllTypes = LazyOf(() =>
        ArkArchitecture.Model.Types.Where(t => t.Namespace != null).ToArray());

    private static readonly Lazy<Class[]> AllClasses = LazyOf(() =>
        ArkArchitecture.Model.Classes.ToArray());

    private static readonly Lazy<Class[]> DomainClasses = LazyOf(() =>
        AllClasses.Value.Where(IsDomain).ToArray());

    private static readonly Lazy<Class[]> ApplicationClasses = LazyOf(() =>
        AllClasses.Value.Where(IsApplication).ToArray());

    internal static string DomainFolder(Class c)
    {
        const string marker = ".Domain.";
        var ns = c.Namespace!.FullName;
        var start = ns.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        return ns[start..].Split('.')[0];
    }

    private static string AggregateNamespaceOf(Class c)
    {
        const string marker = ".Domain.";
        var ns = c.Namespace!.FullName;
        var start = ns.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var folder = ns[start..].Split('.')[0];
        return ns[..(start + folder.Length)];
    }

    private static bool IsAggregateRoot(Class c)
    {
        if (!IsDomain(c) || !IsImplementationKind(c))
        {
            return false;
        }

        var folder = DomainFolder(c);
        if (ExcludedFolders.Contains(folder) || folder is "Events" or "Engines")
        {
            return false;
        }

        var expected = folder.EndsWith("Aggregate", StringComparison.Ordinal)
            ? folder[..^"Aggregate".Length]
            : folder;

        return c.Name == expected;
    }

    private static readonly Lazy<Class[]> Roots = LazyOf(() =>
        DomainClasses.Value.Where(IsAggregateRoot).ToArray());

    private static readonly Lazy<Dictionary<string, Class>> RootByAggregateNamespace = LazyOf(() =>
        Roots.Value.ToDictionary(r => r.Namespace!.FullName, r => r));

    private static bool IsPart(Class c)
    {
        if (!IsDomain(c) || IsAggregateRoot(c) || !IsImplementationKind(c))
        {
            return false;
        }

        if (c.BaseClass?.FullName == "System.Enum")
        {
            return false;
        }

        if (c.Name.EndsWith("Data", StringComparison.Ordinal)
            || c.Name.EndsWith("Dto", StringComparison.Ordinal)
            || c.Name.EndsWith("Command", StringComparison.Ordinal))
        {
            return false;
        }

        var folder = DomainFolder(c);
        if (ExcludedFolders.Contains(folder) || folder is "Events" or "Engines")
        {
            return false;
        }

        return RootByAggregateNamespace.Value.ContainsKey(AggregateNamespaceOf(c));
    }

    private static readonly Lazy<Class[]> Parts = LazyOf(() =>
        DomainClasses.Value.Where(IsPart).ToArray());

    private static readonly Lazy<Class[]> Engines = LazyOf(() =>
        DomainClasses.Value.Where(c => DomainFolder(c) == "Engines").ToArray());

    private static readonly Lazy<Class[]> DomainEvents = LazyOf(() =>
        DomainClasses.Value.Where(c => DomainFolder(c) == "Events").ToArray());

    private static readonly Lazy<Class[]> Services = LazyOf(() =>
        ApplicationClasses.Value
            .Where(c => IsImplementationKind(c) && c.Name.EndsWith("Service", StringComparison.Ordinal))
            .ToArray());

    private static readonly Lazy<Class[]> Orchestrators = LazyOf(() =>
        ApplicationClasses.Value
            .Where(c => IsImplementationKind(c) && c.Name.EndsWith("Orchestrator", StringComparison.Ordinal))
            .ToArray());

    private static readonly Lazy<Class[]> Mappers = LazyOf(() =>
        ApplicationClasses.Value
            .Where(c => IsImplementationKind(c) && c.Name.EndsWith("Mapper", StringComparison.Ordinal))
            .ToArray());

    private static readonly Lazy<Class[]> Handlers = LazyOf(() =>
        ApplicationClasses.Value
            .Where(c => IsImplementationKind(c) && c.ImplementedInterfaces
                .Any(i => i.Name == "INotificationHandler`1"))
            .ToArray());

    private static readonly Lazy<Interface[]> ContractInterfaces = LazyOf(() =>
        ArkArchitecture.Model.Interfaces
            .Where(i => !i.IsCompilerGenerated
                        && i.Namespace != null
                        && ArkArchitecture.InNamespace(i, ArkNamespaces.ApplicationPattern)
                        && i.Namespace.FullName.Contains(".Contracts."))
            .ToArray());

    public static IReadOnlyList<Class> DomainClassesCollection => DomainClasses.Value;
    public static IReadOnlyList<Class> ApplicationClassesCollection => ApplicationClasses.Value;
    public static IReadOnlyList<Class> RootsCollection => Roots.Value;
    public static IReadOnlyList<Class> PartsCollection => Parts.Value;
    public static IReadOnlyList<Class> EnginesCollection => Engines.Value;
    public static IReadOnlyList<Class> DomainEventsCollection => DomainEvents.Value;
    public static IReadOnlyList<Class> ServicesCollection => Services.Value;
    public static IReadOnlyList<Class> OrchestratorsCollection => Orchestrators.Value;
    public static IReadOnlyList<Class> MappersCollection => Mappers.Value;
    public static IReadOnlyList<Class> HandlersCollection => Handlers.Value;
    public static IReadOnlyList<Interface> ContractInterfacesCollection => ContractInterfaces.Value;

    public static IReadOnlyList<Class> ServiceClasses =>
        Services.Value.Concat(Orchestrators.Value).Distinct().ToArray();

    public static IReadOnlyList<Class> RootsAnalyzable =>
        Roots.Value.Concat(Parts.Value).Concat(Engines.Value).Distinct().ToArray();

    public static IReadOnlyList<Class> DomainOf(string context) =>
        DomainClasses.Value.Where(c => ArkArchitecture.ContextOf(c) == context).ToArray();

    public static IReadOnlyList<Class> DomainExcept(params string[] excludedContexts) =>
        DomainClasses.Value
            .Where(c => !excludedContexts.Contains(ArkArchitecture.ContextOf(c)))
            .ToArray();

    public static IReadOnlyList<Class> ServiceClassesOf(string context) =>
        ServiceClasses.Where(c => ArkArchitecture.ContextOf(c) == context).ToArray();

    public static IReadOnlyList<Class> HandlersOf(string context) =>
        Handlers.Value.Where(c => ArkArchitecture.ContextOf(c) == context).ToArray();

    public static IReadOnlyList<Class> RootsOf(string context) =>
        Roots.Value.Where(c => ArkArchitecture.ContextOf(c) == context).ToArray();

    public static IReadOnlyList<Class> PartsOf(string context) =>
        Parts.Value.Where(c => ArkArchitecture.ContextOf(c) == context).ToArray();

    public static Class? RootOf(Class part) =>
        RootByAggregateNamespace.Value.GetValueOrDefault(AggregateNamespaceOf(part));

    public static IReadOnlyList<Class> PartsOf(Class root) =>
        Parts.Value.Where(p => AggregateNamespaceOf(p) == root.Namespace!.FullName).ToArray();

    public static IEnumerable<IType> NonDomainLayers() =>
        AllTypes.Value.Where(t => IsInfrastructure(t) || IsPresentation(t) || IsApplication(t));

    public static IEnumerable<IType> InfrastructureExceptData() =>
        AllTypes.Value.Where(t => IsInfrastructure(t) && !IsData(t));

    public static IEnumerable<IType> PresentationTypes() =>
        AllTypes.Value.Where(IsPresentation);

    public static IEnumerable<IType> DataTypes() =>
        AllTypes.Value.Where(IsData);

    public static IEnumerable<IType> ApplicationTypes() =>
        AllTypes.Value.Where(IsApplication);

    public static IEnumerable<IType> GeneralDomainTypes() =>
        AllTypes.Value.Where(t => IsDomain(t) && ArkArchitecture.ContextOf(t) == ArkArchitecture.GeneralContext);

    public static IEnumerable<IType> DomainTypesAll() =>
        AllTypes.Value.Where(t => ArkArchitecture.InNamespace(t, ArkNamespaces.DomainPattern));

    public static bool DependsOn(Class source, IEnumerable<Class> targets)
    {
        var fullNames = targets.Select(t => t.FullName).Where(n => n != null).ToHashSet();
        return source.Dependencies
            .Select(d => d.Target?.FullName)
            .Any(f => f != null && fullNames.Contains(f));
    }

    public static IReadOnlyList<(string Source, string Target)> ViolatingDependencies(
        IEnumerable<Class> sources, IEnumerable<IType> targets)
    {
        var targetNames = targets.Select(t => t.FullName).Where(n => n != null).ToHashSet();
        var violations = new List<(string, string)>();
        foreach (var s in sources)
        {
            foreach (var d in s.Dependencies)
            {
                var target = d.Target?.FullName;
                if (target == null || target == s.FullName || !targetNames.Contains(target))
                {
                    continue;
                }

                violations.Add((s.FullName, target));
            }
        }

        return violations.Distinct().ToArray();
    }
}