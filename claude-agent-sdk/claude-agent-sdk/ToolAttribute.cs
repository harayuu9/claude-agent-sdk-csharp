using System.Reflection;
using System.Text.Json;

namespace ClaudeAgentSdk;

/// <summary>
/// Attribute for marking methods as MCP tools.
/// This is the C# equivalent of Python's @tool decorator.
/// </summary>
/// <example>
/// <code>
/// public class MyTools
/// {
///     [Tool("greet", "Greet a user by name")]
///     public async Task&lt;SdkMcpToolResult&gt; Greet(GreetArgs args)
///     {
///         return SdkMcpToolResult.FromText($"Hello, {args.Name}!");
///     }
/// }
///
/// // Create server from class with [Tool] attributes
/// var server = SdkMcpServer.FromType&lt;MyTools&gt;("my-tools");
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ToolAttribute : Attribute
{
    /// <summary>
    /// The unique name of the tool.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// A description of what the tool does.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Create a new Tool attribute.
    /// </summary>
    /// <param name="name">Unique identifier for the tool.</param>
    /// <param name="description">Human-readable description of the tool's purpose.</param>
    public ToolAttribute(string name, string description)
    {
        Name = name;
        Description = description;
    }
}

/// <summary>
/// Extension methods for creating MCP servers from types with [Tool] attributes.
/// </summary>
public static class SdkMcpServerExtensions
{
    /// <summary>
    /// Create an MCP server from a type containing methods marked with [Tool] attribute.
    /// </summary>
    /// <typeparam name="T">The type containing tool methods.</typeparam>
    /// <param name="name">Server name.</param>
    /// <param name="version">Server version (default: "1.0.0").</param>
    /// <returns>McpSdkServerConfig ready for use.</returns>
    /// <example>
    /// <code>
    /// public class Calculator
    /// {
    ///     [Tool("add", "Add two numbers")]
    ///     public async Task&lt;SdkMcpToolResult&gt; Add(AddArgs args)
    ///     {
    ///         return SdkMcpToolResult.FromText($"Result: {args.A + args.B}");
    ///     }
    /// }
    ///
    /// var server = SdkMcpServer.FromType&lt;Calculator&gt;("calculator");
    /// </code>
    /// </example>
    public static McpSdkServerConfig FromType<T>(string name, string version = "1.0.0") where T : new()
    {
        return FromInstance(new T(), name, version);
    }

    /// <summary>
    /// Create an MCP server from an instance containing methods marked with [Tool] attribute.
    /// </summary>
    /// <typeparam name="T">The type of the instance.</typeparam>
    /// <param name="instance">The instance containing tool methods.</param>
    /// <param name="name">Server name.</param>
    /// <param name="version">Server version (default: "1.0.0").</param>
    /// <returns>McpSdkServerConfig ready for use.</returns>
    public static McpSdkServerConfig FromInstance<T>(T instance, string name, string version = "1.0.0")
    {
        var tools = new List<ISdkMcpToolDefinition>();
        var type = typeof(T);

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            var toolAttr = method.GetCustomAttribute<ToolAttribute>();
            if (toolAttr == null)
                continue;

            var parameters = method.GetParameters();
            if (parameters.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Tool method '{method.Name}' must have exactly one parameter (the input type).");
            }

            var inputType = parameters[0].ParameterType;
            var returnType = method.ReturnType;

            // Validate return type
            if (!IsValidReturnType(returnType))
            {
                throw new InvalidOperationException(
                    $"Tool method '{method.Name}' must return Task<SdkMcpToolResult>.");
            }

            var tool = CreateToolFromMethod(instance!, method, toolAttr, inputType);
            tools.Add(tool);
        }

        if (tools.Count == 0)
        {
            throw new InvalidOperationException(
                $"Type '{type.Name}' has no methods marked with [Tool] attribute.");
        }

        return SdkMcpServer.Create(name, version, tools);
    }

    private static bool IsValidReturnType(Type returnType)
    {
        if (!returnType.IsGenericType)
            return false;

        var genericDef = returnType.GetGenericTypeDefinition();
        if (genericDef != typeof(Task<>))
            return false;

        var resultType = returnType.GetGenericArguments()[0];
        return resultType == typeof(SdkMcpToolResult);
    }

    private static ISdkMcpToolDefinition CreateToolFromMethod(
        object instance,
        MethodInfo method,
        ToolAttribute attr,
        Type inputType)
    {
        // Use reflection to create the generic tool type
        var toolType = typeof(ReflectionBasedTool<>).MakeGenericType(inputType);
        var tool = Activator.CreateInstance(toolType, instance, method, attr.Name, attr.Description);
        return (ISdkMcpToolDefinition)tool!;
    }
}

/// <summary>
/// Internal tool implementation that uses reflection to call methods.
/// </summary>
internal sealed class ReflectionBasedTool<T> : ISdkMcpToolDefinition
{
    private readonly object _instance;
    private readonly MethodInfo _method;

    public string Name { get; }
    public string Description { get; }
    public object InputSchema { get; }

    public ReflectionBasedTool(object instance, MethodInfo method, string name, string description)
    {
        _instance = instance;
        _method = method;
        Name = name;
        Description = description;
        InputSchema = GenerateSchema();
    }

    public async Task<SdkMcpToolResult> CallAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(arguments);
        var input = JsonSerializer.Deserialize<T>(json)
            ?? throw new InvalidOperationException($"Failed to deserialize arguments to {typeof(T).Name}");

        var task = (Task<SdkMcpToolResult>)_method.Invoke(_instance, [input])!;
        return await task;
    }

    private object GenerateSchema()
    {
        var type = typeof(T);
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var prop in type.GetProperties())
        {
            var propName = JsonNamingPolicy.SnakeCaseLower.ConvertName(prop.Name);
            properties[propName] = GetJsonSchemaType(prop.PropertyType);

            if (prop.PropertyType.IsValueType && Nullable.GetUnderlyingType(prop.PropertyType) == null)
            {
                required.Add(propName);
            }
        }

        return new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required
        };
    }

    private static Dictionary<string, object> GetJsonSchemaType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        if (underlyingType == typeof(string))
            return new Dictionary<string, object> { ["type"] = "string" };
        if (underlyingType == typeof(int) || underlyingType == typeof(long))
            return new Dictionary<string, object> { ["type"] = "integer" };
        if (underlyingType == typeof(float) || underlyingType == typeof(double) || underlyingType == typeof(decimal))
            return new Dictionary<string, object> { ["type"] = "number" };
        if (underlyingType == typeof(bool))
            return new Dictionary<string, object> { ["type"] = "boolean" };

        return new Dictionary<string, object> { ["type"] = "string" };
    }
}
