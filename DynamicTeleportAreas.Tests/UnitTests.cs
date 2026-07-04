using System.Text.RegularExpressions;

using Mono.Cecil;

namespace DynamicTeleportAreas.Tests;

[TestFixture]
public class ArchitectureGuardrailTests
{
    // Nullable backing field preserves warning-free cleanup in OneTimeTearDown
    private AssemblyDefinition? _assemblyBackingField;
    private string[] _reservedHarmonyNames = null!;

    // Strongly-typed gatekeeper property ensures clean, warning-free access across all test blocks
    private AssemblyDefinition Assembly => _assemblyBackingField
        ?? throw new InvalidOperationException("The test assembly context was not properly initialized during OneTimeSetUp.");

    [OneTimeSetUp]
    public void LoadAssemblyMetadata()
    {
        string assemblyPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "DynamicTeleportAreas.dll");
        Assert.That(File.Exists(assemblyPath), Is.True, $"Target assembly missing at path: {assemblyPath}");

        _assemblyBackingField = AssemblyDefinition.ReadAssembly(assemblyPath);
        _reservedHarmonyNames = ["__instance", "__result", "__state", "__args", "__originalMethod", "__runOriginal", "__resultRef"];
    }

    [OneTimeTearDown]
    public void ReleaseAssemblyMetadata()
    {
        // Safe conditional access avoids redundancy warnings on the nullable backing field
        _assemblyBackingField?.Dispose();
    }

    [Test]
    public void Guardrail_1_EnforceStrictInstanceTypeInvariants()
    {
        int checkedParametersCount = 0;

        foreach (var type in Assembly.MainModule.GetTypes())
        {
            var harmonyPatchAttr = type.CustomAttributes
                .FirstOrDefault(a => a.AttributeType.Name == "HarmonyPatch");

            if (harmonyPatchAttr == null || !harmonyPatchAttr.HasConstructorArguments)
                continue;

            var firstArg = harmonyPatchAttr.ConstructorArguments[0];
            if (firstArg.Value is not TypeReference targetComponentType)
                continue;

            string expectedTargetTypeName = targetComponentType.Name;

            var patchMethods = type.Methods.Where(m => m.Name == "Prefix" || m.Name == "Postfix");

            foreach (var method in patchMethods)
            {
                if (!method.HasParameters) continue;

                foreach (var param in method.Parameters)
                {
                    if (param.ParameterType.Name == expectedTargetTypeName)
                    {
                        if (param.Name != "__instance" && (param.Name == "instance" || param.Name == "player" || param.Name.StartsWith('_')))
                        {
                            checkedParametersCount++;
                            Assert.That(param.Name, Is.EqualTo("__instance"),
                                $"Critically Unsound Contract: In patch class '{type.Name}', the method '{method.Name}' " +
                                $"requests a '{expectedTargetTypeName}' type context via the variable name '{param.Name}'. " +
                                $"To inject the intercepted class instance, the parameter name must be exactly '__instance'.");
                        }
                        else if (param.Name == "__instance")
                        {
                            checkedParametersCount++;
                        }
                    }
                }
            }
        }

        Assert.That(checkedParametersCount, Is.GreaterThan(0),
            "Sanity check failed: No instance routing configurations were processed by the inspection loop.");
    }

    [Test]
    public void Guardrail_2_ValidateUnderscoreEncodingPatterns()
    {
        foreach (var type in Assembly.MainModule.GetTypes())
        {
            if (!type.HasCustomAttributes || type.CustomAttributes.All(a => a.AttributeType.Name != "HarmonyPatch"))
                continue;

            var patchMethods = type.Methods.Where(m => m.Name == "Prefix" || m.Name == "Postfix");

            foreach (var method in patchMethods)
            {
                if (!method.HasParameters) continue;

                foreach (var param in method.Parameters)
                {
                    string name = param.Name;
                    string baseName = name.TrimStart('_');
                    int underscoreCount = name.Length - baseName.Length;

                    if (underscoreCount == 0) continue;

                    if (_reservedHarmonyNames.Select(n => n.TrimStart('_')).Contains(baseName))
                    {
                        Assert.That(underscoreCount, Is.EqualTo(2),
                            $"Harmony Structural Defect: In patch class '{type.Name}', method '{method.Name}', " +
                            $"the variable '{name}' contains an invalid prefix length ({underscoreCount}). " +
                            $"The system keyword '{baseName}' must be mapped with exactly 2 underscores (e.g., '__{baseName}').");
                    }
                    else
                    {
                        Assert.That(underscoreCount, Is.EqualTo(3),
                            $"Private Field Binding Defect: In patch class '{type.Name}', method '{method.Name}', " +
                            $"the variable '{name}' contains an invalid prefix length ({underscoreCount}). " +
                            $"Accessing unexposed member fields requires exactly 3 underscores (e.g., '___{baseName}').");
                    }
                }
            }
        }
    }

    [Test]
    public void Guardrail_3_EnforceStaticModifiersOnPatchHooks()
    {
        string[] hookNames = ["Prefix", "Postfix", "Transpiler", "Finalizer"];

        foreach (var type in Assembly.MainModule.GetTypes())
        {
            if (!type.HasCustomAttributes || type.CustomAttributes.All(a => a.AttributeType.Name != "HarmonyPatch"))
                continue;

            foreach (var method in type.Methods)
            {
                if (hookNames.Contains(method.Name))
                {
                    Assert.That(method.IsStatic, Is.True,
                        $"Execution Scope Defect: In patch class '{type.Name}', the routing hook '{method.Name}' " +
                        $"is declared as an instance method. Harmony requires patch hooks to be 'static'.");
                }
            }
        }
    }

    [Test]
    public void Guardrail_4_ValidateBepInExPluginLifecycleEntry()
    {
        var pluginClasses = Assembly.MainModule.GetTypes()
            .Where(t => t.BaseType is { Name: "BaseUnityPlugin" })
            .ToList();

        Assert.That(pluginClasses.Count, Is.EqualTo(1),
            $"Lifecycle Configuration Error: Expected exactly 1 class inheriting from 'BaseUnityPlugin', " +
            $"but discovered {pluginClasses.Count} occurrences.");

        var pluginType = pluginClasses.First();
        var pluginAttribute = pluginType.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "BepInPlugin");

        Assert.That(pluginAttribute, Is.Not.Null,
            $"Metadata Defect: The plugin class '{pluginType.Name}' lacks the mandatory '[BepInPlugin]' attribute.");

        var args = pluginAttribute.ConstructorArguments;
        Assert.That(args.Count, Is.EqualTo(3), "Metadata Defect: Malformed '[BepInPlugin]' constructor.");

        string? pluginGuid = args[0].Value?.ToString();
        string? pluginVersion = args[2].Value?.ToString();

        Assert.That(pluginGuid, Is.Not.Null, "Plugin GUID metadata is null.");
        Assert.That(pluginVersion, Is.Not.Null, "Plugin Version metadata is null.");

        Assert.That(Regex.IsMatch(pluginGuid!, @"^[a-zA-Z0-String\._\-]+$"), Is.True,
            $"Metadata Invalidation: The Plugin GUID '{pluginGuid}' contains illegal characters or spaces.");

        Assert.That(Version.TryParse(pluginVersion!, out _), Is.True,
            $"Metadata Invalidation: The Version string '{pluginVersion}' does not conform to system SemVer standards.");
    }
}