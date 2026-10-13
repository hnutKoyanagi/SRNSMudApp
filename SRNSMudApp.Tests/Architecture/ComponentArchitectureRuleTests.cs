using System.Reflection;

using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using MudBlazor;

using SRNSMudApp.Components;
using SRNSMudApp.Data;
using SRNSMudApp.Extensions;
using SRNSMudApp.Services.Dialogs;

namespace SRNSMudApp.Tests.Architecture;

/// <summary>
///     プロジェクト全体の設計・アーキテクチャ規約遵守を検証するテストクラス。
///     - コンポーネントおよび ViewModel からの ApplicationDbContext 直接インジェクション禁止 (AGENTS.md / ARCH-02)
///     - コンポーネントおよび ViewModel からの IDialogService 直接インジェクション禁止 (IDialogLauncher 経由必須)
///     - ダイアログ用 ViewModel の Transient ライフタイム強制 (STATE-01: 状態汚染防止)
/// </summary>
public class ComponentArchitectureRuleTests
{
    private static readonly HashSet<Type> AllowedDbContextInjectors =
    [
        // 必要に応じて基盤サービスのみ許可
    ];

    private static readonly HashSet<Type> AllowedDialogServiceInjectors =
    [
        // DialogLauncher などのインフラ実装は IDialogService を直接ラップするため除外
        typeof(DialogLauncher)
    ];

    /// <summary>
    ///     全ての Blazor コンポーネントおよび ViewModel において、ApplicationDbContext が
    ///     直接 [Inject] またはコンストラクタ注入されていないことを検証する。
    /// </summary>
    [Fact]
    public void ComponentsAndViewModels_MustNotDirectlyInjectApplicationDbContext()
    {
        var targetTypes = GetAllComponentAndViewModelTypes();
        var violations = new List<string>();

        foreach (var type in targetTypes)
        {
            if (AllowedDbContextInjectors.Contains(type))
            {
                continue;
            }

            // 1. [Inject] プロパティの検査
            var injectedProperties = GetInjectedProperties(type);
            foreach (var prop in injectedProperties)
            {
                if (typeof(DbContext).IsAssignableFrom(prop.PropertyType) ||
                    prop.PropertyType == typeof(ApplicationDbContext))
                {
                    violations.Add($"[Inject Property] '{type.FullName}.{prop.Name}' directly injects '{prop.PropertyType.Name}'. Use IDbContextFactory or IDataProvider instead.");
                }
            }

            // 2. コンストラクタ引数の検査
            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            foreach (var ctor in constructors)
            {
                foreach (var param in ctor.GetParameters())
                {
                    if (typeof(DbContext).IsAssignableFrom(param.ParameterType) ||
                        param.ParameterType == typeof(ApplicationDbContext))
                    {
                        violations.Add($"[Constructor Parameter] '{type.FullName}(...{param.Name}...)' directly injects '{param.ParameterType.Name}'. Use IDbContextFactory or IDataProvider instead.");
                    }
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Found {violations.Count} direct DbContext injection violation(s):{Environment.NewLine}" +
            string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    ///     全ての Blazor コンポーネントおよび ViewModel において、MudBlazor の IDialogService が
    ///     直接注入されず、IDialogLauncher を経由していることを検証する。
    /// </summary>
    [Fact]
    public void ComponentsAndViewModels_MustNotDirectlyInjectIDialogService()
    {
        var targetTypes = GetAllComponentAndViewModelTypes();
        var violations = new List<string>();

        foreach (var type in targetTypes)
        {
            if (AllowedDialogServiceInjectors.Contains(type))
            {
                continue;
            }

            // 1. [Inject] プロパティの検査
            var injectedProperties = GetInjectedProperties(type);
            foreach (var prop in injectedProperties)
            {
                if (prop.PropertyType == typeof(IDialogService))
                {
                    violations.Add($"[Inject Property] '{type.FullName}.{prop.Name}' injects IDialogService. Use IDialogLauncher instead.");
                }
            }

            // 2. コンストラクタ引数の検査
            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            foreach (var ctor in constructors)
            {
                foreach (var param in ctor.GetParameters())
                {
                    if (param.ParameterType == typeof(IDialogService))
                    {
                        violations.Add($"[Constructor Parameter] '{type.FullName}(...{param.Name}...)' injects IDialogService. Use IDialogLauncher instead.");
                    }
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Found {violations.Count} direct IDialogService injection violation(s):{Environment.NewLine}" +
            string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    ///     ダイアログで使用される ViewModel は、ダイアログを閉じて再オープンした際の状態汚染を防ぐため、
    ///     必ず Transient ライフタイムで登録されていることを検証する (STATE-01)。
    /// </summary>
    [Fact]
    public void DialogViewModels_MustBeRegisteredAsTransient()
    {
        var services = new ServiceCollection();
        services.AddViewModels();

        var dialogViewModelTypes = services
            .Where(d => d.ServiceType.Name.Contains("Dialog", StringComparison.OrdinalIgnoreCase) ||
                        d.ServiceType.Name.Contains("CreateEdit", StringComparison.OrdinalIgnoreCase) ||
                        d.ServiceType.Name.Contains("Members", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var nonTransientDialogs = dialogViewModelTypes
            .Where(d => d.Lifetime != ServiceLifetime.Transient)
            .Select(d => $"{d.ServiceType.FullName} (Registered as {d.Lifetime}, expected Transient)")
            .ToList();

        Assert.True(
            nonTransientDialogs.Count == 0,
            $"Found {nonTransientDialogs.Count} dialog ViewModel(s) not registered as Transient:{Environment.NewLine}" +
            string.Join(Environment.NewLine, nonTransientDialogs));
    }

    private static List<Type> GetAllComponentAndViewModelTypes()
    {
        var assemblies = new[]
        {
            typeof(App).Assembly,
            typeof(SRNSMudApp.Client._Imports).Assembly
        };

        return assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => typeof(IComponent).IsAssignableFrom(t) ||
                        t.Name.EndsWith("ViewModel", StringComparison.Ordinal) ||
                        (t.Namespace != null && t.Namespace.Contains("ViewModel", StringComparison.Ordinal)))
            .Distinct()
            .ToList();
    }

    private static IEnumerable<PropertyInfo> GetInjectedProperties(Type type)
    {
        var current = type;
        var seen = new HashSet<string>();
        while (current != null && current != typeof(object))
        {
            var properties = current.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            foreach (var property in properties)
            {
                if (property.GetCustomAttributes(typeof(InjectAttribute), inherit: true).Length != 0)
                {
                    if (seen.Add($"{property.DeclaringType?.FullName}.{property.Name}"))
                    {
                        yield return property;
                    }
                }
            }
            current = current.BaseType;
        }
    }
}