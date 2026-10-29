//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Analyzers.Localizability;

/// <summary>
/// Localizability analyzer: flags hardcoded user-facing strings
/// with JLNLOC001 at suggestion severity — UI text assignments, exception
/// messages, validation messages outside the localized attributes, and view-dictionary
/// entries. Everything outside the curated shapes (identifiers, logging calls,
/// test asserts, local initializers) is quiet by construction. Standard Roslyn
/// suppression (pragma, <c>SuppressMessage</c>, <c>dotnet_diagnostic</c> severity,
/// per-folder editorconfig) applies; generated code is never analyzed.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LocalizabilityAnalyzer : DiagnosticAnalyzer {
    /// <summary>
    /// The MSBuild property (via analyzer config) raising the rule severity
    /// (<c>suggestion</c>, <c>warning</c>, <c>error</c>); repositories opt into
    /// warnings with it, and the false-positive review runs under it.
    /// </summary>
    internal const string SEVERITY_PROPERTY = "build_property.LocalizabilitySeverity";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [LocalizabilityDiagnostics.HardcodedString];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context) {
        if(context is null) {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static compilationContext => {
            var severity = ResolveSeverity(compilationContext.Options.AnalyzerConfigOptionsProvider.GlobalOptions);
            compilationContext.RegisterOperationAction(operationContext => AnalyzeAssignment(operationContext, severity), OperationKind.SimpleAssignment); compilationContext.RegisterOperationAction(operationContext => AnalyzeObjectCreation(operationContext, severity), OperationKind.ObjectCreation);
            compilationContext.RegisterOperationAction(operationContext => AnalyzeInvocation(operationContext, severity), OperationKind.Invocation);
            compilationContext.RegisterOperationAction(operationContext => AnalyzeAttribute(operationContext, severity), OperationKind.Attribute);
        });
    }

    /// <summary>
    /// Analyzes assignment.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="severity">The severity.</param>
    private static void AnalyzeAssignment(OperationAnalysisContext context, DiagnosticSeverity severity) {
        var assignment = (IAssignmentOperation)context.Operation;

        // Dynamic view bags (ViewBag.Title = "...") do not bind to operations:
        // match them syntactically on the receiver name.
        if(assignment.Target.Syntax is MemberAccessExpressionSyntax dynamicAccess &&
           dynamicAccess.Expression is IdentifierNameSyntax dynamicReceiver &&
           dynamicReceiver.Identifier.Text.Equals("ViewBag", StringComparison.Ordinal) &&
           FindUserString(assignment.Value) is { } dynamicValue) {
            Report(context, severity, dynamicValue, "Hardcoded view-dictionary text. Move it to a catalog and resolve it through IMessageLocalizer.");
            return;
        }

        if(assignment.Target is not IPropertyReferenceOperation property) {
            return;
        }

        // View-dictionary indexers: ViewData["Title"] = "...".
        if(property.Arguments.Length > 0 && property.Instance is not null && IsViewBagReceiver(property.Instance)) {
            if(FindUserString(assignment.Value) is { } entry) {
                Report(context, severity, entry, "Hardcoded view-dictionary text. Move it to a catalog and resolve it through IMessageLocalizer.");
            }

            return;
        }

        if(!IsUiTextProperty(property.Property.Name) ||
           property.Instance is null ||
           !WellKnownNames.DerivesFromUiBase(property.Instance.Type)) {
            return;
        }

        if(FindUserString(assignment.Value) is { } literal) {
            Report(context, severity, literal, $"Hardcoded UI text assigned to '{property.Property.Name}'. Move it to a catalog and resolve it through IMessageLocalizer.");
        }
    }

    /// <summary>
    /// Analyzes object creation.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="severity">The severity.</param>
    private static void AnalyzeObjectCreation(OperationAnalysisContext context, DiagnosticSeverity severity) {
        var creation = (IObjectCreationOperation)context.Operation;
        var typeName = creation.Type?.OriginalDefinition?.ToDisplayString() ?? creation.Type?.ToDisplayString();
        if(typeName is null) {
            return;
        }

        var simpleName = SimpleName(typeName);
        var isValidationResult = simpleName.Equals("ValidationResult", StringComparison.Ordinal);
        if(!isValidationResult && !WellKnownNames.DerivesFromException(creation.Type)) {
            return;
        }

        if(FirstStringLiteral(creation.Arguments) is { } literal) {
            Report(
                context,
                severity,
                literal,
                isValidationResult
                    ? "Hardcoded validation message outside the localized attributes. Move it to a catalog and resolve it through IMessageLocalizer."
                    : $"Hardcoded exception message in '{simpleName}'. Move user-facing text to a catalog and resolve it through IMessageLocalizer; suppress with justification when the throw is internal-only.");
        }
    }

    /// <summary>
    /// Analyzes invocation.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="severity">The severity.</param>
    private static void AnalyzeInvocation(OperationAnalysisContext context, DiagnosticSeverity severity) {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if(method is null) {
            return;
        }

        // MessageBox.Show("...") and Results/TypedResults.Problem("...").
        if((IsMessageBoxShow(invocation) || IsProblemCall(invocation)) &&
           FirstStringLiteral(invocation.Arguments) is { } message) {
            Report(context, severity, message, $"Hardcoded message in '{method.Name}'. Move it to a catalog and resolve it through IMessageLocalizer.");
            return;
        }

        // ModelState.AddModelError(key, "..."): the message is the second argument.
        if(IsModelStateAddError(invocation)) {
            var messageArgument = invocation.Arguments.Length > 1 ? invocation.Arguments[1] : null;
            if(StringLiteral(messageArgument?.Value) is { } errorText) {
                Report(context, severity, errorText, "Hardcoded validation message in 'AddModelError'. Move it to a catalog and resolve it through IMessageLocalizer.");
            }
        }
    }

    /// <summary>
    /// Analyzes attribute.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="severity">The severity.</param>
    private static void AnalyzeAttribute(OperationAnalysisContext context, DiagnosticSeverity severity) {
        var attribute = (IAttributeOperation)context.Operation;
        if(attribute.Syntax is not AttributeSyntax syntax) {
            return;
        }

        var model = attribute.SemanticModel;
        var attributeClass = model is null
            ? null
            : (model.GetSymbolInfo(syntax, context.CancellationToken).Symbol as IMethodSymbol)?.ContainingType;
        if(attributeClass is null) {
            return;
        }

        var simpleName = SimpleName(attributeClass.OriginalDefinition?.ToDisplayString() ?? attributeClass.ToDisplayString());
        if(attribute.Operation is not IObjectCreationOperation creation) {
            return;
        }

        var reported = new HashSet<IOperation>();
        AnalyzeCreationArguments(context, severity, simpleName, creation, reported);
        AnalyzeNamedArguments(context, severity, simpleName, attribute, reported);

        if(IsDisplayAttribute(simpleName) &&
           FirstStringLiteral(creation.Arguments) is { } positional &&
           reported.Add(positional)) {
            Report(context, severity, positional, $"Hardcoded '{simpleName}' display text. Move it to a catalog and resolve it through IMessageLocalizer.");
        }
    }

    /// <summary>
    /// Reports hardcoded <c>ErrorMessage</c> creation arguments of a validation attribute.
    /// </summary>
    /// <param name="context">The operation analysis context.</param>
    /// <param name="severity">The configured severity.</param>
    /// <param name="simpleName">The attribute's simple name.</param>
    /// <param name="creation">The attribute's object creation.</param>
    /// <param name="reported">The literals already reported for this attribute.</param>
    private static void AnalyzeCreationArguments(
        OperationAnalysisContext context,
        DiagnosticSeverity severity,
        string simpleName,
        IObjectCreationOperation creation,
        HashSet<IOperation> reported) {
        foreach(var argument in creation.Arguments) {
            var parameterName = argument.Parameter?.Name;
            var literal = StringLiteral(argument.Value);
            if(literal is null) {
                continue;
            }

            if(parameterName?.Equals("ErrorMessage", StringComparison.Ordinal) == true && IsErrorMessageAttribute(simpleName)) {
                Report(context, severity, literal, $"Hardcoded '{simpleName}' validation message. Use the localized validation attributes or move the text to a catalog.");
                reported.Add(literal);
            }
        }
    }

    /// <summary>
    /// Reports hardcoded named arguments of a validation or display attribute; they surface as
    /// nested assignments to the property (e.g. <c>[Required(ErrorMessage = "...")]</c>), not as
    /// creation arguments.
    /// </summary>
    /// <param name="context">The operation analysis context.</param>
    /// <param name="severity">The configured severity.</param>
    /// <param name="simpleName">The attribute's simple name.</param>
    /// <param name="attribute">The attribute operation.</param>
    /// <param name="reported">The literals already reported for this attribute.</param>
    private static void AnalyzeNamedArguments(
        OperationAnalysisContext context,
        DiagnosticSeverity severity,
        string simpleName,
        IAttributeOperation attribute,
        HashSet<IOperation> reported) {
        foreach(var descendant in attribute.Operation.Descendants()) {
            if(descendant is not IAssignmentOperation assignment ||
               assignment.Target is not IPropertyReferenceOperation property ||
               StringLiteral(assignment.Value) is not { } literal ||
               !reported.Add(literal)) {
                continue;
            }

            var propertyName = property.Property.Name;
            if(propertyName.Equals("ErrorMessage", StringComparison.Ordinal) && IsErrorMessageAttribute(simpleName)) {
                Report(context, severity, literal, $"Hardcoded '{simpleName}' validation message. Use the localized validation attributes or move the text to a catalog.");
            }

            if((propertyName.Equals("Name", StringComparison.OrdinalIgnoreCase) || propertyName.Equals("Description", StringComparison.OrdinalIgnoreCase)) &&
               IsDisplayAttribute(simpleName)) {
                Report(context, severity, literal, $"Hardcoded '{simpleName}' display text. Move it to a catalog and resolve it through IMessageLocalizer.");
            }
        }
    }

    /// <summary>
    /// Determines whether the value is display attribute.
    /// </summary>
    /// <param name="simpleName">The simple name.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsDisplayAttribute(string simpleName) =>
        simpleName.Equals("DisplayAttribute", StringComparison.Ordinal) ||
        simpleName.Equals("DisplayNameAttribute", StringComparison.Ordinal) ||
        simpleName.Equals("Display", StringComparison.Ordinal) ||
        simpleName.Equals("DisplayName", StringComparison.Ordinal);

    /// <summary>
    /// Determines whether the value is ui text property.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsUiTextProperty(string name) =>
        WellKnownNames.UiTextProperties.Any(candidate => name.Equals(candidate, StringComparison.Ordinal));

    /// <summary>
    /// Determines whether the value is view bag receiver.
    /// </summary>
    /// <param name="instance">The instance.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsViewBagReceiver(IOperation instance) {
        if(instance is IMemberReferenceOperation member &&
           (member.Member.Name.Equals("ViewData", StringComparison.Ordinal) ||
            member.Member.Name.Equals("ViewBag", StringComparison.Ordinal))) {
            return true;
        }

        var typeName = instance.Type?.OriginalDefinition?.ToDisplayString() ?? instance.Type?.ToDisplayString();
        if(typeName is null) {
            return false;
        }

        var simple = SimpleName(typeName);
        return WellKnownNames.ViewBagReceivers.Any(receiver => simple.Equals(receiver, StringComparison.Ordinal));
    }

    /// <summary>
    /// Determines whether the value is message box show.
    /// </summary>
    /// <param name="invocation">The invocation.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsMessageBoxShow(IInvocationOperation invocation) =>
        invocation.TargetMethod.Name.Equals("Show", StringComparison.Ordinal) &&
        invocation.TargetMethod.ContainingType?.Name.Equals("MessageBox", StringComparison.Ordinal) == true;

    /// <summary>
    /// Determines whether the value is problem call.
    /// </summary>
    /// <param name="invocation">The invocation.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsProblemCall(IInvocationOperation invocation) =>
        invocation.TargetMethod.Name.Equals("Problem", StringComparison.Ordinal);

    /// <summary>
    /// Determines whether the value is model state add error.
    /// </summary>
    /// <param name="invocation">The invocation.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsModelStateAddError(IInvocationOperation invocation) {
        var method = invocation.TargetMethod;
        if(!method.Name.Equals("AddModelError", StringComparison.Ordinal)) {
            return false;
        }

        var containing = method.ContainingType?.OriginalDefinition?.ToDisplayString() ?? method.ContainingType?.ToDisplayString();
        return containing is not null && SimpleName(containing).Equals("ModelStateDictionary", StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines whether the value is error message attribute.
    /// </summary>
    /// <param name="simpleName">The simple name.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsErrorMessageAttribute(string simpleName) =>
        WellKnownNames.ErrorMessageAttributes.Any(candidate => simpleName.Equals(candidate, StringComparison.Ordinal));

    /// <summary>
    /// Performs the simple name operation.
    /// </summary>
    /// <param name="fullName">The full name.</param>
    /// <returns>The simple name text.</returns>
    private static string SimpleName(string fullName) {
        var dot = fullName.LastIndexOf('.');
        return dot < 0 ? fullName : fullName.Substring(dot + 1);
    }

    /// <summary>
    /// Finds user string.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The user string result.</returns>
    private static IOperation? FindUserString(IOperation value) {
        var current = value;
        while(true) {
            switch(current) {
                case ILiteralOperation { ConstantValue: { HasValue: true, Value: string text } } when text.Length > 0:
                    return current;
                case IInterpolatedStringOperation:
                    return current;
                case IBinaryOperation binary when binary.OperatorKind == BinaryOperatorKind.Add:
                    if(ContainsStringLiteral(binary.LeftOperand) || ContainsStringLiteral(binary.RightOperand)) {
                        return current;
                    }

                    return null;
                case IInvocationOperation invocation when IsFormatLike(invocation):
                    return FirstStringLiteral(invocation.Arguments) is not null ? current : null;
                case IConversionOperation conversion:
                    current = conversion.Operand;
                    continue;
                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// Determines whether the value contains string literal.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool ContainsStringLiteral(IOperation operation) {
        if(operation is ILiteralOperation { ConstantValue: { HasValue: true, Value: string text } } && text.Length > 0) {
            return true;
        }

        if(operation is IBinaryOperation binary && binary.OperatorKind == BinaryOperatorKind.Add) {
            return ContainsStringLiteral(binary.LeftOperand) || ContainsStringLiteral(binary.RightOperand);
        }

        if(operation is IConversionOperation conversion) {
            return ContainsStringLiteral(conversion.Operand);
        }

        return false;
    }

    /// <summary>
    /// Determines whether the value is format like.
    /// </summary>
    /// <param name="invocation">The invocation.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsFormatLike(IInvocationOperation invocation) {
        var method = invocation.TargetMethod;
        if(!method.Name.Equals("Format", StringComparison.Ordinal) && !method.Name.Equals("Concat", StringComparison.Ordinal)) {
            return false;
        }

        var containing = method.ContainingType?.OriginalDefinition?.ToDisplayString() ?? method.ContainingType?.ToDisplayString();
        return containing is not null && SimpleName(containing).Equals("String", StringComparison.Ordinal);
    }

    /// <summary>
    /// Unwraps implicit conversions and returns a non-empty string literal, if any.
    /// Framework signatures are nullable-annotated (<c>string?</c>), so even plain
    /// literals arrive wrapped in conversions.
    /// </summary>
    /// <param name="operation">The operation to unwrap.</param>
    /// <returns>The non-empty literal text, or <see langword="null"/> when the operation is not one.</returns>
    private static IOperation? StringLiteral(IOperation? operation) {
        var current = operation;
        while(current is IConversionOperation conversion) {
            current = conversion.Operand;
        }

        return current is ILiteralOperation { ConstantValue: { HasValue: true, Value: string text } } && text.Length > 0
            ? current
            : null;
    }

    /// <summary>
    /// Performs the first string literal operation.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The first string literal result.</returns>
    private static IOperation? FirstStringLiteral(IEnumerable<IArgumentOperation> arguments) =>
        arguments.Select(static argument => argument.Value).Select(StringLiteral).FirstOrDefault(static value => value is not null);

    /// <summary>
    /// Performs the report operation.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="severity">The severity.</param>
    /// <param name="literal">The literal.</param>
    /// <param name="message">The message.</param>
    private static void Report(OperationAnalysisContext context, DiagnosticSeverity severity, IOperation literal, string message) {
        var diagnostic = severity == LocalizabilityDiagnostics.HardcodedString.DefaultSeverity
            ? Diagnostic.Create(LocalizabilityDiagnostics.HardcodedString, literal.Syntax?.GetLocation() ?? Location.None, message)
            : Diagnostic.Create(LocalizabilityDiagnostics.HardcodedString, literal.Syntax?.GetLocation() ?? Location.None, severity, additionalLocations: null, properties: null, message);
        context.ReportDiagnostic(diagnostic);
    }

    /// <summary>
    /// Resolves severity.
    /// </summary>
    /// <param name="options">The options instance.</param>
    /// <returns>The severity result.</returns>
    internal static DiagnosticSeverity ResolveSeverity(AnalyzerConfigOptions options) {
        if(options.TryGetValue(SEVERITY_PROPERTY, out var configured)) {
            if(string.Equals(configured, "error", StringComparison.OrdinalIgnoreCase)) {
                return DiagnosticSeverity.Error;
            }

            if(string.Equals(configured, "warning", StringComparison.OrdinalIgnoreCase)) {
                return DiagnosticSeverity.Warning;
            }

            if(string.Equals(configured, "suggestion", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(configured, "hidden", StringComparison.OrdinalIgnoreCase)) {
                return DiagnosticSeverity.Hidden;
            }
        }

        return LocalizabilityDiagnostics.HardcodedString.DefaultSeverity;
    }
}
