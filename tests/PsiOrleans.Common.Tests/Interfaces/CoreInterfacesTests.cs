using FluentAssertions;
using PsiOrleans.Common.Interfaces;
using PsiOrleans.Common.Models;
using System.Reflection;
using Xunit;

namespace PsiOrleans.Common.Tests.Interfaces;

public class CoreInterfacesTests
{
    [Fact]
    public void IAgentContext_ShouldBeInterface()
    {
        // Arrange
        var interfaceType = typeof(IAgentContext);
        
        // Assert
        interfaceType.IsInterface.Should().BeTrue();
        interfaceType.Namespace.Should().Be("PsiOrleans.Common.Interfaces");
    }

    [Fact]
    public void IAgentContext_ShouldHaveAgentIdProperty()
    {
        // Arrange
        var interfaceType = typeof(IAgentContext);
        
        // Act
        var property = interfaceType.GetProperty("AgentId");
        
        // Assert
        property.Should().NotBeNull();
        property!.PropertyType.Should().Be<AgentId>();
        property.CanRead.Should().BeTrue();
        property.CanWrite.Should().BeFalse(); // Should be readonly
    }

    [Fact]
    public void IAgentContext_ShouldHaveAgentNameProperty()
    {
        // Arrange
        var interfaceType = typeof(IAgentContext);
        
        // Act
        var property = interfaceType.GetProperty("AgentName");
        
        // Assert
        property.Should().NotBeNull();
        property!.PropertyType.Should().Be<string>();
        property.CanRead.Should().BeTrue();
    }

    [Fact]
    public void IAgentContext_ShouldHaveRoleProperty()
    {
        // Arrange
        var interfaceType = typeof(IAgentContext);
        
        // Act
        var property = interfaceType.GetProperty("Role");
        
        // Assert
        property.Should().NotBeNull();
        property!.PropertyType.Should().Be<AgentRole>();
        property.CanRead.Should().BeTrue();
    }

    [Fact]
    public void ITaskAnalyzer_ShouldBeInterface()
    {
        // Arrange
        var interfaceType = typeof(ITaskAnalyzer);
        
        // Assert
        interfaceType.IsInterface.Should().BeTrue();
        interfaceType.Namespace.Should().Be("PsiOrleans.Common.Interfaces");
    }

    [Fact]
    public void ITaskAnalyzer_ShouldHaveAnalyzeTaskAsyncMethod()
    {
        // Arrange
        var interfaceType = typeof(ITaskAnalyzer);
        
        // Act
        var method = interfaceType.GetMethod("AnalyzeTaskAsync");
        
        // Assert
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be<Task<TaskAnalysisResult>>();
        
        var parameters = method.GetParameters();
        parameters.Should().HaveCount(3);
        parameters[0].ParameterType.Should().Be<string>(); // task description
        parameters[1].ParameterType.Should().Be<IAgentContext>(); // context
        parameters[2].ParameterType.Should().Be<AgentConfiguration>(); // configuration
    }



    [Fact]
    public void IOrchestrator_ShouldBeInterface()
    {
        // Arrange
        var interfaceType = typeof(IOrchestrator);
        
        // Assert
        interfaceType.IsInterface.Should().BeTrue();
        interfaceType.Namespace.Should().Be("PsiOrleans.Common.Interfaces");
    }

    [Fact]
    public void IOrchestrator_ShouldHaveCoordinateAgentsAsyncMethod()
    {
        // Arrange
        var interfaceType = typeof(IOrchestrator);
        
        // Act
        var method = interfaceType.GetMethod("CoordinateAgentsAsync");
        
        // Assert
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be<Task<OrchestratorResult>>();
        
        var parameters = method.GetParameters();
        parameters.Should().HaveCount(3);
        parameters[0].ParameterType.Should().Be<IEnumerable<AgentId>>(); // agent ids
        parameters[1].ParameterType.Should().Be<string>(); // task description
        parameters[2].ParameterType.Should().Be<IAgentContext>(); // context
    }

    [Fact]
    public void IOrchestrator_ShouldHaveAssignTaskAsyncMethod()
    {
        // Arrange
        var interfaceType = typeof(IOrchestrator);
        
        // Act
        var method = interfaceType.GetMethod("AssignTaskAsync");
        
        // Assert
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be<Task<bool>>();
        
        var parameters = method.GetParameters();
        parameters.Should().HaveCount(3);
        parameters[0].ParameterType.Should().Be<AgentId>(); // target agent
        parameters[1].ParameterType.Should().Be<string>(); // task description
        parameters[2].ParameterType.Should().Be<IAgentContext>(); // context
    }

    [Fact]
    public void ISpecializedExecutor_ShouldBeInterface()
    {
        // Arrange
        var interfaceType = typeof(ISpecializedExecutor);
        
        // Assert
        interfaceType.IsInterface.Should().BeTrue();
        interfaceType.Namespace.Should().Be("PsiOrleans.Common.Interfaces");
    }

    [Fact]
    public void ISpecializedExecutor_ShouldHaveExecuteTaskAsyncMethod()
    {
        // Arrange
        var interfaceType = typeof(ISpecializedExecutor);
        
        // Act
        var method = interfaceType.GetMethod("ExecuteTaskAsync");
        
        // Assert
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be<Task<ExecutionResult>>();
        
        var parameters = method.GetParameters();
        parameters.Should().HaveCount(2);
        parameters[0].ParameterType.Should().Be<string>(); // task description
        parameters[1].ParameterType.Should().Be<IAgentContext>(); // context
    }

    [Fact]
    public void ISpecializedExecutor_ShouldHaveCanHandleTaskMethod()
    {
        // Arrange
        var interfaceType = typeof(ISpecializedExecutor);
        
        // Act
        var method = interfaceType.GetMethod("CanHandleTask");
        
        // Assert
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be<bool>();
        
        var parameters = method.GetParameters();
        parameters.Should().HaveCount(2);
        parameters[0].ParameterType.Should().Be<string>(); // task description
        parameters[1].ParameterType.Should().Be<IAgentContext>(); // context
    }

    [Fact]
    public void ISpecializedExecutor_ShouldHaveSpecializationProperty()
    {
        // Arrange
        var interfaceType = typeof(ISpecializedExecutor);
        
        // Act
        var property = interfaceType.GetProperty("Specialization");
        
        // Assert
        property.Should().NotBeNull();
        property!.PropertyType.Should().Be<string>();
        property.CanRead.Should().BeTrue();
    }
} 