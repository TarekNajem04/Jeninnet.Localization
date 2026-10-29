//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Core.MissingKeys;

/// <summary>
/// Captures the first external call-site frame for a reported miss: the
/// first stack frame outside the shipped library type names and the
/// <c>System.*</c> runtime infrastructure (async builders, thread pool,
/// execution context), rendered as <c>Type.Method</c>. Matching runs on full
/// type names (reliable across state-machine frames); test, sample, and
/// application code is never skipped — only the pipeline's own library roots.
/// File/line info is deliberately not captured (no PDB dependence);
/// asynchronous callers surface as their state-machine frames. Called lazily
/// and only when a sink is present: the disabled path never pays for a stack walk.
/// </summary>
internal static class MissingKeyCallSite {
    /// <summary>
    /// The shipped library roots (the project identity rule: project == root namespace).
    /// </summary>
    private static readonly string[] _pipelineNamespaces = [
        "Jeninnet.Localization.Abstractions",
        "Jeninnet.Localization.Core",
        "Jeninnet.Localization.Storage",
        "Jeninnet.Localization.Providers.",
        "Jeninnet.Localization.Translation.",
        "Jeninnet.Localization.Integrations.",
        "Jeninnet.Localization.Interchange.",
        "Jeninnet.Localization.Generators.",
        "Jeninnet.Localization.Tools.",
    ];

    /// <summary>
    /// Captures the call site, or <see langword="null"/> when no external frame exists.
    /// </summary>
    /// <returns>The call site, or <see langword="null"/>.</returns>
    internal static string? Capture() =>
        new StackTrace(fNeedFileInfo: false).GetFrames()
            .Select(TryGetExternalSite)
            .FirstOrDefault(static site => site is not null);

    /// <summary>
    /// Attempts to get external site.
    /// </summary>
    /// <param name="frame">The frame.</param>
    /// <returns>The get external site result.</returns>
#if !NET10_0_OR_GREATER
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Missing-key call-site capture is optional and never required for localization; net10+ uses DiagnosticMethodInfo.")]
#endif
    private static string? TryGetExternalSite(StackFrame? frame) {
        if(frame is null) {
            return null;
        }

#if NET10_0_OR_GREATER
        // DiagnosticMethodInfo.Create is the trim/AOT-safe equivalent of
        // StackFrame.GetMethod() (the analyzer-recommended replacement); it resolves
        // the frame's method name without requiring preserved metadata graphs.
        var method = DiagnosticMethodInfo.Create(frame);
        var declaringTypeName = method?.DeclaringTypeName;
        var methodName = method?.Name;
#else
        var method = frame.GetMethod();
        var declaringTypeName = method?.DeclaringType?.FullName;
        var methodName = method?.Name;
#endif
        if(method is null || string.IsNullOrEmpty(declaringTypeName) || string.IsNullOrEmpty(methodName)) {
            return null;
        }

        if(declaringTypeName.StartsWith("System.", StringComparison.Ordinal) || IsPipelineName(declaringTypeName)) {
            return null;
        }

        return declaringTypeName + "." + methodName;
    }

    /// <summary>
    /// Determines whether the value is pipeline name.
    /// </summary>
    /// <param name="fullName">The full name.</param>
    /// <returns>true when the condition holds; otherwise, false.</returns>
    private static bool IsPipelineName(string fullName) =>
        _pipelineNamespaces.Any(root =>
            fullName.Equals(root, StringComparison.Ordinal) ||
            fullName.StartsWith(root + ".", StringComparison.Ordinal));
}
