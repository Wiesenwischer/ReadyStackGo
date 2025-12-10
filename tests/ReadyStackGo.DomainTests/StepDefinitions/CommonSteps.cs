namespace ReadyStackGo.DomainTests.StepDefinitions;

using FluentAssertions;
using Reqnroll;
using ReadyStackGo.DomainTests.Support;

/// <summary>
/// Common step definitions shared across multiple features.
/// </summary>
[Binding]
public class CommonSteps
{
    private readonly TestContext _context;

    public CommonSteps(TestContext context)
    {
        _context = context;
    }

    [Then(@"the operation should fail with error ""(.*)""")]
    public void ThenTheOperationShouldFailWithError(string expectedError)
    {
        _context.LastException.Should().NotBeNull("Expected an exception to be thrown");
        _context.LastException!.Message.Should().Contain(expectedError);
    }
}
