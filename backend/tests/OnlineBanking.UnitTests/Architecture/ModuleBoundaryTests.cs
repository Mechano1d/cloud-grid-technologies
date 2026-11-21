using System.Reflection;
using NetArchTest.Rules;
using OnlineBanking.Module.Accounts.Domain;
using OnlineBanking.Module.Clients.Domain;
using OnlineBanking.Module.Transactions.Domain;
using OnlineBanking.Shared.Results;

namespace OnlineBanking.UnitTests.Architecture;

public sealed class ModuleBoundaryTests
{
    private static readonly Assembly SharedAssembly = typeof(Result).Assembly;
    private static readonly Assembly ClientsAssembly = typeof(Client).Assembly;
    private static readonly Assembly AccountsAssembly = typeof(Account).Assembly;
    private static readonly Assembly TransactionsAssembly = typeof(Transaction).Assembly;
    private static readonly Assembly InfrastructureAssembly = Assembly.Load("OnlineBanking.Infrastructure");
    private static readonly Assembly PersistenceAssembly = Assembly.Load("OnlineBanking.Persistence");
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    private static readonly Assembly[] AllMonolithAssemblies =
    [
        SharedAssembly,
        InfrastructureAssembly,
        PersistenceAssembly,
        ClientsAssembly,
        AccountsAssembly,
        TransactionsAssembly,
        ApiAssembly
    ];

    [Fact]
    public void Shared_MustNotDependOnAspNetCore_EfCore_OrModules()
    {
        var result = Types.InAssembly(SharedAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.AspNetCore",
                "Microsoft.EntityFrameworkCore",
                "Npgsql",
                "OnlineBanking.Infrastructure",
                "OnlineBanking.Persistence",
                "OnlineBanking.Module.Clients",
                "OnlineBanking.Module.Accounts",
                "OnlineBanking.Module.Transactions")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailingTypes(result));
    }

    [Fact]
    public void ClientsModule_MustNotDependOnOtherModulesOrPersistence()
    {
        var result = Types.InAssembly(ClientsAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "OnlineBanking.Module.Accounts",
                "OnlineBanking.Module.Transactions",
                "OnlineBanking.Persistence")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailingTypes(result));
    }

    [Fact]
    public void AccountsModule_MustNotDependOnOtherModulesOrPersistence()
    {
        var result = Types.InAssembly(AccountsAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "OnlineBanking.Module.Clients",
                "OnlineBanking.Module.Transactions",
                "OnlineBanking.Persistence")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailingTypes(result));
    }

    [Fact]
    public void TransactionsModule_MustNotDependOnOtherModulesOrPersistence()
    {
        var result = Types.InAssembly(TransactionsAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "OnlineBanking.Module.Clients",
                "OnlineBanking.Module.Accounts",
                "OnlineBanking.Persistence")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailingTypes(result));
    }

    [Theory]
    [MemberData(nameof(ModuleAssemblies))]
    public void DomainLayer_MustNotDependOnApi_Application_AspNetCore_OrEfCore(Assembly moduleAssembly, string domainNamespace)
    {
        var result = Types.InAssembly(moduleAssembly)
            .That()
            .ResideInNamespace(domainNamespace)
            .ShouldNot()
            .HaveDependencyOnAny(
                $"{moduleAssembly.GetName().Name}.Api",
                $"{moduleAssembly.GetName().Name}.Application",
                "Microsoft.AspNetCore",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailingTypes(result));
    }

    [Fact]
    public void Solution_MustNotReferenceMediatROrAutoMapper()
    {
        foreach (var assembly in AllMonolithAssemblies)
        {
            var referenced = assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty).ToList();
            Assert.DoesNotContain(referenced, name => name.StartsWith("MediatR", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(referenced, name => name.StartsWith("AutoMapper", StringComparison.OrdinalIgnoreCase));
        }

        var typeCheck = Types.InAssemblies(AllMonolithAssemblies)
            .ShouldNot()
            .HaveDependencyOnAny("MediatR", "AutoMapper")
            .GetResult();

        Assert.True(typeCheck.IsSuccessful, FormatFailingTypes(typeCheck));
    }

    public static TheoryData<Assembly, string> ModuleAssemblies() => new()
    {
        { ClientsAssembly, "OnlineBanking.Module.Clients.Domain" },
        { AccountsAssembly, "OnlineBanking.Module.Accounts.Domain" },
        { TransactionsAssembly, "OnlineBanking.Module.Transactions.Domain" }
    };

    private static string FormatFailingTypes(TestResult result) =>
        result.FailingTypeNames is { Count: > 0 }
            ? $"Failing types: {string.Join(", ", result.FailingTypeNames)}"
            : string.Empty;
}
