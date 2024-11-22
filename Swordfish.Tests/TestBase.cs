using System;
using DryIoc;
using Xunit.Abstractions;

namespace Swordfish.Tests;

public abstract class TestBase : IDisposable
{
    protected readonly ITestOutputHelper Output;
    
    private readonly Container _container;

    public TestBase(ITestOutputHelper output)
    {
        Output = output;
        _container = new Container();
        
        Setup();
    }
    
    private void Setup()
    {
        SetupContainer(_container);
        _container.ValidateAndThrow();
        OnSetup();
    }

    void IDisposable.Dispose()
    {
        OnTearDown();
        _container.Dispose();
    }

    protected virtual void SetupContainer(Container container) {}
    protected virtual void OnSetup() {}
    protected virtual void OnTearDown() {}
}
