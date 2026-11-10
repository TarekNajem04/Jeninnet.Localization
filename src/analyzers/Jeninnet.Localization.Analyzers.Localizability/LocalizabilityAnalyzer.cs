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
/// entries — plus composite shapes (JLNLOC002 concatenated fragments, JLNLOC003
/// uninterpolated holes) and catalog-backed findings (JLNLOC004 placeholder
/// mismatches, JLNLOC005 missing keys, JLNLOC006 unused keys) from the
/// generator's catalog inputs. Everything outside the curated shapes (identifiers,
/// logging calls, test asserts, local initializers) is quiet by construction.
/// Standard Roslyn suppression (pragma, <c>SuppressMessage</c>,
/// <c>dotnet_diagnostic</c> severity, per-folder editorconfig) applies; generated
/// code is never analyzed.
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
        [
            LocalizabilityDiagnostics.HardcodedString,
            LocalizabilityDiagnostics.ConcatenatedFragments,
            LocalizabilityDiagnostics.NonLocalizedInterpolation,
            LocalizabilityDiagnostics.PlaceholderMismatch,
            LocalizabilityDiagnostics.MissingCatalogKey,
            LocalizabilityDiagnostics.UnusedCatalogKey,
        ];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context) {
        if(context is null) {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(compilationContext => {
            var options = compilationContext.Options.AnalyzerConfigOptionsProvider.GlobalOptions;
            var state = CatalogRuleState.Create(compilationContext.Options.AdditionalFiles, compilationContext.CancellationToken);
            compilationContext.RegisterOperationAction(operationContext => AnalyzeAssignment(operationContext, ResolveSeverity(options, LocalizabilityDiagnostics.HardcodedString)), OperationKind.SimpleAssignment); compilationContext.RegisterOperationAction(operationContext => AnalyzeObjectCreation(operationContext, ResolveSeverity(options, LocalizabilityDiagnostics.HardcodedString)), OperationKind.ObjectCreation);
            compilationContext.RegisterOperationAction(operationContext => AnalyzeInvocation(operationContext, ResolveSeverity(options, LocalizabilityDiagnostics.HardcodedString), state), OperationKind.Invocation);
            compilationContext.RegisterOperationAction(operationContext => AnalyzeAttribute(operationContext, ResolveSeverity(options, LocalizabilityDiagnostics.HardcodedString)), OperationKind.Attribute);
            compilationContext.RegisterOperationAction(operationContext => AnalyzeFieldReference(operationContext, state), OperationKind.FieldReference);
            compilationContext.RegisterOperationAction(operationContext => AnalyzePropertyReference(operationContext, state), OperationKind.PropertyReference);
            compilationContext.RegisterCompilationEndAction(endContext => ReportUnusedKeys(endContext, state));
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
            ReportCompositeShape(context, dynamicValue);
            return;
        }

        if(assignment.Target is not IPropertyReferenceOperation property) {
            return;
        }

        // View-dictionary indexers: ViewData["Title"] = "...".
        if(property.Arguments.Length > 0 && property.Instance is not null && IsViewBagReceiver(property.Instance)) {
            if(FindUserString(assignment.Value) is { } entry) {
                Report(context, severity, entry, "Hardcoded view-dictionary text. Move it to a catalog and resolve it through IMessageLocalizer.");
                ReportCompositeShape(context, entry);
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
            ReportCompositeShape(context, literal);
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

        foreach(var argument in creation.Arguments) {
            ReportCompositeShape(context, argument.Value);
        }
    }

    /// <summary>
    /// Analyzes invocation.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="severity">The severity.</param>
    /// <param name="state">The catalog rule state.</param>
    private static void AnalyzeInvocation(OperationAnalysisContext context, DiagnosticSeverity severity, CatalogRuleState state) {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if(method is null) {
            return;
        }

        AnalyzeFormatCall(context, invocation, state);
        AnalyzeKeyPositionInvocation(context, invocation, state);

        // MessageBox.Show("...") and Results/TypedResults.Problem("...").
        if((IsMessageBoxShow(invocation) || IsProblemCall(invocation)) &&
           FirstStringLiteral(invocation.Arguments) is { } message) {
            Report(context, severity, message, $"Hardcoded message in '{method.Name}'. Move it to a catalog and resolve it through IMessageLocalizer.");
            ReportCompositeShape(context, invocation.Arguments.FirstOrDefault()?.Value);
            return;
        }

        // ModelState.AddModelError(key, "..."): the message is the second argument.
        if(IsModelStateAddError(invocation)) {
            var messageArgument = invocation.Arguments.Length > 1 ? invocation.Arguments[1] : null;
            if(StringLiteral(messageArgument?.Value) is { } errorText) {
                Report(context, severity, errorText, "Hardcoded validation message in 'AddModelError'. Move it to a catalog and resolve it through IMessageLocalizer.");
            }

            ReportCompositeShape(context, messageArgument?.Value);
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
    /// <returns>true when the operation resembles string formatting.</returns>
    private static bool IsFormatLike(IInvocationOperation invocation) {
        var method = invocation.TargetMethod;
        if(method is null) {
            return false;
        }

        if(!method.Name.Equals("Format", StringComparison.Ordinal) && !method.Name.Equals("Concat", StringComparison.Ordinal)) {
            return false;
        }

        // Metadata name, not the display string: ToDisplayString renders the
        // string keyword for System.String and would never equal "String".
        return method.ContainingType?.Name.Equals("String", StringComparison.Ordinal) is true;
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
    internal static DiagnosticSeverity ResolveSeverity(AnalyzerConfigOptions options) =>
        ResolveSeverity(options, LocalizabilityDiagnostics.HardcodedString);

    /// <summary>
    /// Resolves severity for one descriptor.
    /// </summary>
    /// <param name="options">The options instance.</param>
    /// <param name="descriptor">The descriptor.</param>
    /// <returns>The severity result.</returns>
    internal static DiagnosticSeverity ResolveSeverity(AnalyzerConfigOptions options, DiagnosticDescriptor descriptor) {
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

        return descriptor.DefaultSeverity;
    }

    /// <summary>
    /// Reports composite shapes for JLNLOC002/JLNLOC003: a <c>+</c> chain containing a
    /// string literal, or an interpolated string, flowing into an already-flagged sink.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="value">The flagged sink value.</param>
    private static void ReportCompositeShape(OperationAnalysisContext context, IOperation? value) {
        var current = value;
        while(current is IConversionOperation conversion) {
            current = conversion.Operand;
        }

        if(current is IBinaryOperation binary &&
            binary.OperatorKind == BinaryOperatorKind.Add &&
            ContainsStringLiteral(binary)) {
            ReportDescriptor(
                context,
                LocalizabilityDiagnostics.ConcatenatedFragments,
                binary.Syntax?.GetLocation() ?? Location.None,
                "Concatenated user-facing fragments. Build one template instead of '+' chains; the code fix converts this to an interpolated string, then move it to a catalog.");
        } else if(current is IInterpolatedStringOperation interpolated) {
            ReportDescriptor(
                context,
                LocalizabilityDiagnostics.NonLocalizedInterpolation,
                interpolated.Syntax?.GetLocation() ?? Location.None,
                "Interpolated user-facing string outside the message pipeline. Move the template to a catalog and resolve it through IMessageLocalizer.");
        }
    }

    /// <summary>
    /// Analyzes a string.Format call for JLNLOC004: when the first argument resolves to
    /// a catalog key, its placeholders must be fillable by the supplied arguments.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="invocation">The invocation.</param>
    /// <param name="state">The catalog rule state.</param>
    private static void AnalyzeFormatCall(OperationAnalysisContext context, IInvocationOperation invocation, CatalogRuleState state) {
        if(!IsFormatLike(invocation) || invocation.Arguments.Length == 0) {
            return;
        }

        var key = ResolveKeyConstant(invocation.Arguments[0].Value);
        if(key is null) {
            return;
        }

        state.AddReference(key);
        if(!state.HasKeys) {
            return;
        }

        if(!state.Templates.TryGetValue(key, out var template)) {
            ReportDescriptor(
                context,
                LocalizabilityDiagnostics.MissingCatalogKey,
                invocation.Arguments[0].Syntax?.GetLocation() ?? Location.None,
                $"Key '{key}' is not served by any visible catalog. Add it to a catalog or fix the reference.");
            return;
        }

        var supplied = invocation.Arguments.Length - 1;
        var missing = new List<string>();
        foreach(var identifier in CatalogKeyReader.PlaceholderIds(template)) {
            if(IsPositionalPlaceholder(identifier)) {
                if(!int.TryParse(identifier, out var index) || index >= supplied) {
                    missing.Add("{" + identifier + "}");
                }
            } else {
                missing.Add(identifier);
            }
        }

        if(missing.Count > 0) {
            ReportDescriptor(
                context,
                LocalizabilityDiagnostics.PlaceholderMismatch,
                invocation.Syntax?.GetLocation() ?? Location.None,
                $"string.Format for key '{key}' cannot fill the catalog template's placeholder(s): {string.Join(", ", missing)}. The catalog template is '{template}'.");
        }
    }

    /// <summary>
    /// Records key-position arguments for JLNLOC005/JLNLOC006 accounting.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="invocation">The invocation.</param>
    /// <param name="state">The catalog rule state.</param>
    private static void AnalyzeKeyPositionInvocation(OperationAnalysisContext context, IInvocationOperation invocation, CatalogRuleState state) {
        var name = invocation.TargetMethod?.Name;
        if(name is null) {
            return;
        }

        var takesSingular = name.Equals("GetMessageTemplateAsync", StringComparison.Ordinal);
        var takesPair = name.Equals("GetPluralTemplateAsync", StringComparison.Ordinal) ||
            name.Equals("GetPluralTemplateWithContextAsync", StringComparison.Ordinal);
        if(!takesSingular && !takesPair) {
            return;
        }

        if(!DeclaresMessageProvider(invocation.TargetMethod?.ContainingType)) {
            return;
        }

        RecordKeyPosition(context, invocation.Arguments.Length > 0 ? invocation.Arguments[0].Value : null, state);
        if(takesPair) {
            RecordKeyPosition(context, invocation.Arguments.Length > 1 ? invocation.Arguments[1].Value : null, state);
        }
    }

    /// <summary>
    /// Records a key-position value: a *Keys constant or a string literal.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="value">The argument value.</param>
    /// <param name="state">The catalog rule state.</param>
    private static void RecordKeyPosition(OperationAnalysisContext context, IOperation? value, CatalogRuleState state) {
        var key = ResolveKeyConstant(value);
        if(key is null) {
            return;
        }

        state.AddReference(key);
        if(state.HasKeys && !state.Keys.Contains(key)) {
            ReportDescriptor(
                context,
                LocalizabilityDiagnostics.MissingCatalogKey,
                value?.Syntax?.GetLocation() ?? Location.None,
                $"Key '{key}' is not served by any visible catalog. Add it to a catalog or fix the reference.");
        }
    }

    /// <summary>
    /// Records *Keys constant references for JLNLOC005/JLNLOC006 accounting.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="state">The catalog rule state.</param>
    private static void AnalyzeFieldReference(OperationAnalysisContext context, CatalogRuleState state) {
        if(context.Operation is not IFieldReferenceOperation reference ||
            reference.Field is not IFieldSymbol field ||
            !field.IsConst ||
            field.Type?.SpecialType != SpecialType.System_String ||
            field.ContainingType?.Name.EndsWith("Keys", StringComparison.Ordinal) != true) {
            return;
        }

        if(field.ConstantValue as string is not { Length: > 0 }) {
            return;
        }

        RecordKeyPosition(context, context.Operation, state);
    }

    /// <summary>
    /// Records localizer indexer keys for JLNLOC005/JLNLOC006 accounting.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="state">The catalog rule state.</param>
    private static void AnalyzePropertyReference(OperationAnalysisContext context, CatalogRuleState state) {
        if(context.Operation is not IPropertyReferenceOperation property ||
            property.Arguments.Length == 0 ||
            !IsStringLocalizer(property.Property?.ContainingType)) {
            return;
        }

        RecordKeyPosition(context, property.Arguments[0].Value, state);
    }

    /// <summary>
    /// Reports catalog keys never referenced in the compilation (JLNLOC006).
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="state">The catalog rule state.</param>
    private static void ReportUnusedKeys(CompilationAnalysisContext context, CatalogRuleState state) {
        if(!state.HasKeys) {
            return;
        }

        var referenced = state.ReferencedSnapshot();
        foreach(var pair in state.Catalogs.OrderBy(static entry => entry.Key, StringComparer.Ordinal)) {
            foreach(var key in pair.Value.Keys.OrderBy(static entry => entry, StringComparer.Ordinal)) {
                if(referenced.Contains(key)) {
                    continue;
                }

                var line = pair.Value.Lines.TryGetValue(key, out var found) ? found : 1;
                context.ReportDiagnostic(Diagnostic.Create(
                    LocalizabilityDiagnostics.UnusedCatalogKey,
                    Location.Create(
                        pair.Key,
                        default,
                        new LinePositionSpan(
                            new LinePosition(line - 1, 0),
                            new LinePosition(line - 1, 0))),
                    $"Catalog key '{key}' is never referenced in this compilation. Remove it or reference it through a key constant."));
            }
        }
    }

    /// <summary>
    /// Resolves a key constant: a const string field value or a string literal.
    /// </summary>
    /// <param name="value">The operation value.</param>
    /// <returns>The key, or null when the value is not a resolvable key.</returns>
    private static string? ResolveKeyConstant(IOperation? value) {
        var current = value;
        while(current is IConversionOperation conversion) {
            current = conversion.Operand;
        }

        if(current is IFieldReferenceOperation reference &&
            reference.Field is IFieldSymbol field &&
            field.IsConst &&
            field.Type?.SpecialType == SpecialType.System_String) {
            return field.ConstantValue as string;
        }

        if(StringLiteral(current) is { } literal &&
            literal.Syntax is LiteralExpressionSyntax syntax &&
            syntax.Token.ValueText is { Length: > 0 } text) {
            return text;
        }

        return null;
    }

    /// <summary>
    /// Determines whether the type is a string localizer indexer host.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>true when the indexer host is a string localizer.</returns>
    private static bool IsStringLocalizer(ITypeSymbol? type) {
        if(type is null) {
            return false;
        }

        if(type.Name.Equals("IStringLocalizer", StringComparison.Ordinal)) {
            return true;
        }

        return type.AllInterfaces.Any(static candidate => candidate.Name.Equals("IStringLocalizer", StringComparison.Ordinal));
    }

    /// <summary>
    /// Determines whether the type declares message-provider lookup methods.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>true when the type is a message provider.</returns>
    private static bool DeclaresMessageProvider(ITypeSymbol? type) {
        if(type is null) {
            return false;
        }

        if(type.Name.EndsWith("MessageProvider", StringComparison.Ordinal)) {
            return true;
        }

        return type.AllInterfaces.Any(static candidate => candidate.Name.Equals("IMessageProvider", StringComparison.Ordinal));
    }

    /// <summary>
    /// Determines whether the identifier is a positional placeholder.
    /// </summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns>true when every character is an ASCII digit.</returns>
    private static bool IsPositionalPlaceholder(string identifier) =>
        identifier.Length > 0 && identifier.All(static character => character is >= '0' and <= '9');

    /// <summary>
    /// Reports a non-JLNLOC001 descriptor at a location with severity resolution.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="descriptor">The descriptor.</param>
    /// <param name="location">The location.</param>
    /// <param name="message">The message.</param>
    private static void ReportDescriptor(OperationAnalysisContext context, DiagnosticDescriptor descriptor, Location location, string message) =>
        context.ReportDiagnostic(Diagnostic.Create(descriptor, location, ResolveSeverity(context.Options.AnalyzerConfigOptionsProvider.GlobalOptions, descriptor), additionalLocations: null, properties: null, message));
}
