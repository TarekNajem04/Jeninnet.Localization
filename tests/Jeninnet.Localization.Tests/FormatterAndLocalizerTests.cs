//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the formatter and localizer tests.
/// </summary>
[TestClass]
public class FormatterAndLocalizerTests {
    private readonly DefaultMessageFormatter _formatter = new();

    /// <summary>
    /// Verifies that format replaces single parameter.
    /// </summary>
    [TestMethod]
    public void Format_ReplacesSingleParameter() {
        const string template = "The field '{PropertyName}' is required.";
        var parameters = new Dictionary<string, object?> { ["PropertyName"] = "Age" };

        var result = _formatter.Format(template, parameters);

        result.Should().Be("The field 'Age' is required.");
    }

    /// <summary>
    /// Verifies that format replaces multiple parameters.
    /// </summary>
    [TestMethod]
    public void Format_ReplacesMultipleParameters() {
        const string template = "'{PropertyName}' must be between {Min} and {Max}.";
        var parameters = new Dictionary<string, object?> {
            ["PropertyName"] = "Age",
            ["Min"] = 18,
            ["Max"] = 60
        };

        var result = _formatter.Format(template, parameters);

        result.Should().Be("'Age' must be between 18 and 60.");
    }

    /// <summary>
    /// Verifies that format leaves unknown parameters is unchanged.
    /// </summary>
    [TestMethod]
    public void Format_LeavesUnknownParametersUnchanged() {
        const string template = "Value {Unknown} remains.";
        var parameters = new Dictionary<string, object?> { ["Other"] = "x" };

        var result = _formatter.Format(template, parameters);

        result.Should().Be("Value {Unknown} remains.");
    }

    /// <summary>
    /// Verifies that format handles null parameter value.
    /// </summary>
    [TestMethod]
    public void Format_HandlesNullParameterValue() {
        const string template = "Value is {Value}.";
        var parameters = new Dictionary<string, object?> { ["Value"] = null };

        var result = _formatter.Format(template, parameters);

        result.Should().Be("Value is .");
    }

    /// <summary>
    /// Verifies that format handles empty template.
    /// </summary>
    [TestMethod]
    public void Format_HandlesEmptyTemplate() {
        const string template = "";
        var parameters = new Dictionary<string, object?> { ["Key"] = "value" };

        var result = _formatter.Format(template, parameters);

        result.Should().Be("");
    }

    /// <summary>
    /// Verifies that format handles template without placeholders.
    /// </summary>
    [TestMethod]
    public void Format_HandlesTemplateWithoutPlaceholders() {
        const string template = "Static message.";
        var parameters = new Dictionary<string, object?> { ["Key"] = "value" };

        var result = _formatter.Format(template, parameters);

        result.Should().Be("Static message.");
    }

    /// <summary>
    /// Verifies that format uses to string for non string values.
    /// </summary>
    [TestMethod]
    public void Format_UsesToStringForNonStringValues() {
        const string template = "Count is {Count}.";
        var parameters = new Dictionary<string, object?> { ["Count"] = 42 };

        var result = _formatter.Format(template, parameters);

        result.Should().Be("Count is 42.");
    }

    /// <summary>
    /// Verifies that format throws on null template.
    /// </summary>
    [TestMethod]
    public void Format_ThrowsOnNullTemplate() {
        var act = () => _formatter.Format(null!, new Dictionary<string, object?>());

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that format throws on null parameters.
    /// </summary>
    [TestMethod]
    public void Format_ThrowsOnNullParameters() {
        var act = () => _formatter.Format("Value is {Value}.", null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
