using System.Reflection;
using System.Reflection.Emit;
using Microsoft.Extensions.DependencyInjection;
using NetArchTest.Rules;

namespace Gocgun.Mediator.Tests;

/// <summary>Registry attacks and the Results boundary.</summary>
[Collection(PipelineCacheCollection.Name)]
public sealed class AdversarialMediatorTests
{
    /// <summary>Two handlers for one request fail startup validation.</summary>
    [Fact]
    public void Two_handlers_fail_validation()
    {
        var assembly = ProbeAssembly.Create(handlerCount: 2);
        var services = new ServiceCollection();
        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddMediator(validateHandlerRegistry: true, assembly));
        Assert.Contains("exactly one handler", exception.Message, StringComparison.Ordinal);
        Assert.Contains("found 2", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>A request with no handler fails validation.</summary>
    [Fact]
    public void Zero_handlers_fail_validation()
    {
        var assembly = ProbeAssembly.Create(handlerCount: 0);
        var services = new ServiceCollection();
        var exception = Assert.Throws<InvalidOperationException>(() =>
            HandlerRegistryValidator.Validate(services, [assembly]));
        Assert.Contains("found 0", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>One handler passes validation.</summary>
    [Fact]
    public void One_handler_passes_validation()
    {
        var assembly = ProbeAssembly.Create(handlerCount: 1);
        var services = new ServiceCollection();
        services.AddMediator(validateHandlerRegistry: true, assembly);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ISender));
    }

    /// <summary>A closed behavior type is rejected.</summary>
    [Fact]
    public void Closed_behavior_type_throws()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() => services.AddOpenBehavior(typeof(ThrowingBehavior)));
    }

    /// <summary>A null behavior type is rejected.</summary>
    [Fact]
    public void Null_behavior_type_throws()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() => services.AddOpenBehavior(null!));
    }

    /// <summary>The package does not reference Gocgun.Results.</summary>
    [Fact]
    public void Assembly_does_not_reference_results()
    {
        var names = typeof(ISender).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name);
        Assert.DoesNotContain(names, name => string.Equals(name, "Gocgun.Results", StringComparison.Ordinal));
        var architecture = Types.InAssembly(typeof(ISender).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Gocgun.Results")
            .GetResult();
        Assert.True(architecture.IsSuccessful);
    }

    private static class ProbeAssembly
    {
        public static Assembly Create(int handlerCount)
        {
            var assembly = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("Gocgun.Mediator.Probe" + Guid.NewGuid().ToString("N")),
                AssemblyBuilderAccess.Run);
            var module = assembly.DefineDynamicModule("probe");
            var request = module.DefineType("ProbeRequest", TypeAttributes.Public | TypeAttributes.Sealed);
            request.AddInterfaceImplementation(typeof(IRequest<string>));
            var requestType = request.CreateType();
            var handlerService = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(string));
            var interfaceMethod = handlerService.GetMethod("HandleAsync")!;
            for (var index = 0; index < handlerCount; index++)
            {
                var handler = module.DefineType("ProbeHandler" + index.ToString(System.Globalization.CultureInfo.InvariantCulture), TypeAttributes.Public | TypeAttributes.Sealed);
                handler.AddInterfaceImplementation(handlerService);
                var method = handler.DefineMethod(
                    "HandleAsync",
                    MethodAttributes.Public | MethodAttributes.Virtual,
                    typeof(ValueTask<string>),
                    [requestType, typeof(CancellationToken)]);
                var il = method.GetILGenerator();
                il.Emit(OpCodes.Ldstr, "ok");
                il.Emit(OpCodes.Call, typeof(ValueTask).GetMethod(nameof(ValueTask.FromResult))!.MakeGenericMethod(typeof(string)));
                il.Emit(OpCodes.Ret);
                handler.DefineMethodOverride(method, interfaceMethod);
                handler.CreateType();
            }

            return assembly;
        }
    }
}
