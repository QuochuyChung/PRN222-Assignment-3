using System.Reflection;
using AiInterview.Common.Configuration;
using AiInterview.Data;
using AiInterview.Services.Dashboard;

namespace AiInterview.Tests.Architecture;

/// <summary>
/// Khóa ranh giới 3 lớp bằng máy: <c>Web → Services → Data → Common</c>.
/// Test này đỏ thì build đỏ — không dựa vào lời dặn.
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly Web = typeof(Program).Assembly;
    private static readonly Assembly Services = typeof(IDashboardService).Assembly;
    private static readonly Assembly Data = typeof(AppDbContext).Assembly;
    private static readonly Assembly Common = typeof(InfraOptions).Assembly;

    private const string DataNamespace = "AiInterview.Data";

    private static bool References(Assembly from, Assembly to)
        => from.GetReferencedAssemblies().Any(r => r.Name == to.GetName().Name);

    [Fact]
    public void Web_Does_Not_Reference_Data()
        => Assert.False(References(Web, Data), "Web không được tham chiếu Data: PageModel chỉ được gọi Service.");

    [Fact]
    public void Services_Does_Not_Reference_Web()
        => Assert.False(References(Services, Web), "Services không được tham chiếu ngược lên Web.");

    [Fact]
    public void Data_Does_Not_Reference_Services_Or_Web()
    {
        Assert.False(References(Data, Services), "Data không được tham chiếu ngược lên Services.");
        Assert.False(References(Data, Web), "Data không được tham chiếu ngược lên Web.");
    }

    [Fact]
    public void Common_Does_Not_Reference_Other_Layers()
    {
        Assert.False(References(Common, Data));
        Assert.False(References(Common, Services));
        Assert.False(References(Common, Web));
    }

    [Fact]
    public void Services_Public_Api_Does_Not_Expose_Data_Types()
    {
        var violations = Services.GetExportedTypes()
            .SelectMany(type => SignatureTypes(type).Select(used => $"{type.FullName} dùng {used.FullName}"))
            .Distinct()
            .ToList();

        Assert.True(
            violations.Count == 0,
            "Service phải trả DTO (và enum ở Common), không để lộ kiểu của lớp Data:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void Web_Types_Do_Not_Depend_On_Repositories_Or_Data()
    {
        var violations = Web.GetTypes()
            .SelectMany(type => SignatureTypes(type, includeNonPublic: true)
                .Where(used => IsDataType(used) || used.Name.EndsWith("Repository", StringComparison.Ordinal))
                .Select(used => $"{type.FullName} dùng {used.FullName}"))
            .Distinct()
            .ToList();

        Assert.True(
            violations.Count == 0,
            "PageModel/Hub chỉ được dùng Service:\n" + string.Join("\n", violations));
    }

    private static bool IsDataType(Type type)
        => type.Namespace is { } ns && (ns == DataNamespace || ns.StartsWith(DataNamespace + ".", StringComparison.Ordinal));

    /// <summary>Mọi kiểu xuất hiện trong chữ ký của <paramref name="type"/> (cha, interface, constructor, method, property, field) thuộc lớp Data.</summary>
    private static IEnumerable<Type> SignatureTypes(Type type, bool includeNonPublic = false)
    {
        var flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly | BindingFlags.Public;
        if (includeNonPublic)
        {
            flags |= BindingFlags.NonPublic;
        }

        var used = new List<Type>();

        if (type.BaseType is not null) used.Add(type.BaseType);
        used.AddRange(type.GetInterfaces());

        foreach (var ctor in type.GetConstructors(flags))
            used.AddRange(ctor.GetParameters().Select(p => p.ParameterType));

        foreach (var method in type.GetMethods(flags))
        {
            used.Add(method.ReturnType);
            used.AddRange(method.GetParameters().Select(p => p.ParameterType));
        }

        foreach (var property in type.GetProperties(flags)) used.Add(property.PropertyType);
        foreach (var field in type.GetFields(flags)) used.Add(field.FieldType);

        return used.SelectMany(Flatten).Where(IsDataType);
    }

    private static IEnumerable<Type> Flatten(Type type)
    {
        if (type.HasElementType)
        {
            foreach (var inner in Flatten(type.GetElementType()!)) yield return inner;
            yield break;
        }

        yield return type;

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
                foreach (var inner in Flatten(argument))
                    yield return inner;
        }
    }
}
