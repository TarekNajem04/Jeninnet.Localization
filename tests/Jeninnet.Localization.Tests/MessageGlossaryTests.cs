//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Contains the message glossary tests.
/// </summary>
[TestClass]
public class MessageGlossaryTests {
    private static readonly CultureInfo _french = CultureInfo.GetCultureInfo("fr");
    private static readonly CultureInfo _german = CultureInfo.GetCultureInfo("de");

    /// <summary>
    /// Verifies that register valid term is accepted and is listed.
    /// </summary>
    [TestMethod]
    public void Register_ValidTerm_IsAcceptedAndListed() {
        var glossary = new MessageGlossary();

        var result = glossary.Register("Jeninnet", _french, "JeninnetFR");

        result.Registered.Should().BeTrue();
        result.Conflicts.Should().BeEmpty();
        glossary.Terms.Should().ContainSingle();
        var term = glossary.Terms[0];
        term.SourceTerm.Should().Be("Jeninnet");
        term.TargetCulture.Should().Be(_french);
        term.Target.Should().Be("JeninnetFR");
        term.CaseSensitive.Should().BeFalse();
        term.WholeWord.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that register identical term is idempotent.
    /// </summary>
    [TestMethod]
    public void Register_IdenticalTerm_IsIdempotent() {
        var glossary = new MessageGlossary();

        glossary.Register("Jeninnet", _french, "JeninnetFR").Registered.Should().BeTrue();
        var second = glossary.Register("Jeninnet", _french, "JeninnetFR");

        second.Registered.Should().BeTrue("an identical re-registration is idempotent");
        second.Conflicts.Should().BeEmpty();
        glossary.Terms.Should().ContainSingle();
    }

    /// <summary>
    /// Verifies that register duplicate term with different target reports conflict and is not is applied.
    /// </summary>
    [TestMethod]
    public void Register_DuplicateTermWithDifferentTarget_ReportsConflictAndIsNotApplied() {
        var glossary = new MessageGlossary();
        glossary.Register("Hello", _french, "Bonjour");

        var result = glossary.Register("Hello", _french, "Salut");

        result.Registered.Should().BeFalse();
        result.Conflicts.Should().ContainSingle();
        var conflict = result.Conflicts[0];
        conflict.Kind.Should().Be(GlossaryConflictKind.DuplicateTerm);
        conflict.Existing.Target.Should().Be("Bonjour");
        conflict.Rejected.Target.Should().Be("Salut");
        glossary.Terms.Should().ContainSingle();
        glossary.Terms[0].Target.Should().Be("Bonjour", "a conflicting term must never be applied silently");
    }

    /// <summary>
    /// Verifies that register duplicate term with different rules reports conflict.
    /// </summary>
    [TestMethod]
    public void Register_DuplicateTermWithDifferentRules_ReportsConflict() {
        var glossary = new MessageGlossary();
        glossary.Register("Hello", _french, "Bonjour", wholeWord: true);

        var result = glossary.Register("Hello", _french, "Bonjour", wholeWord: false);

        result.Registered.Should().BeFalse();
        result.Conflicts.Should().ContainSingle();
        result.Conflicts[0].Kind.Should().Be(GlossaryConflictKind.DuplicateTerm);
        glossary.Terms.Should().ContainSingle();
    }

    /// <summary>
    /// Verifies that register term contained in existing term reports overlap conflict.
    /// </summary>
    [TestMethod]
    public void Register_TermContainedInExistingTerm_ReportsOverlapConflict() {
        var glossary = new MessageGlossary();
        glossary.Register("New York", _french, "New YorkFR");

        var result = glossary.Register("York", _french, "YorkFR");

        result.Registered.Should().BeFalse();
        result.Conflicts.Should().ContainSingle();
        result.Conflicts[0].Kind.Should().Be(GlossaryConflictKind.OverlappingTerms);
        result.Conflicts[0].Existing.SourceTerm.Should().Be("New York");
        result.Conflicts[0].Rejected.SourceTerm.Should().Be("York");
        glossary.Terms.Should().ContainSingle();
    }

    /// <summary>
    /// Verifies that register existing term contained in new term reports overlap conflict.
    /// </summary>
    [TestMethod]
    public void Register_ExistingTermContainedInNewTerm_ReportsOverlapConflict() {
        var glossary = new MessageGlossary();
        glossary.Register("York", _french, "YorkFR");

        var result = glossary.Register("New York", _french, "New YorkFR");

        result.Registered.Should().BeFalse();
        result.Conflicts.Should().ContainSingle();
        result.Conflicts[0].Kind.Should().Be(GlossaryConflictKind.OverlappingTerms);
        glossary.Terms.Should().ContainSingle();
    }

    /// <summary>
    /// Verifies that register word boundary prevents false overlap.
    /// </summary>
    [TestMethod]
    public void Register_WordBoundaryPreventsFalseOverlap() {
        var glossary = new MessageGlossary();
        glossary.Register("York", _french, "YorkFR", wholeWord: true);

        var result = glossary.Register("Newyorkian", _french, "NewyorkianFR", wholeWord: true);

        result.Registered.Should().BeTrue("a contained substring that cannot match at a word boundary is not an overlap");
        glossary.Terms.Should().HaveCount(2);
    }

    /// <summary>
    /// Verifies that register case insensitive comparison detects cross case overlap.
    /// </summary>
    [TestMethod]
    public void Register_CaseInsensitiveComparison_DetectsCrossCaseOverlap() {
        var glossary = new MessageGlossary();
        glossary.Register("york", _french, "YorkFR");

        var result = glossary.Register("New York", _french, "New YorkFR");

        result.Registered.Should().BeFalse();
        result.Conflicts.Should().ContainSingle();
        result.Conflicts[0].Kind.Should().Be(GlossaryConflictKind.OverlappingTerms);
    }

    /// <summary>
    /// Verifies that register case sensitive terms are distinct.
    /// </summary>
    [TestMethod]
    public void Register_CaseSensitiveTerms_AreDistinct() {
        var glossary = new MessageGlossary();

        glossary.Register("York", _french, "YorkFR", caseSensitive: true).Registered.Should().BeTrue();
        var result = glossary.Register("york", _french, "yorkFR", caseSensitive: true);

        result.Registered.Should().BeTrue("case-sensitive terms are compared ordinally, so case variants do not conflict");
        glossary.Terms.Should().HaveCount(2);
    }

    /// <summary>
    /// Verifies that register non whole word term inside word reports overlap conflict.
    /// </summary>
    [TestMethod]
    public void Register_NonWholeWordTermInsideWord_ReportsOverlapConflict() {
        var glossary = new MessageGlossary();
        glossary.Register("category", _french, "categorieFR");

        var result = glossary.Register("cat", _french, "chatFR", wholeWord: false);

        result.Registered.Should().BeFalse();
        result.Conflicts.Should().ContainSingle();
        result.Conflicts[0].Kind.Should().Be(GlossaryConflictKind.OverlappingTerms);
    }

    /// <summary>
    /// Verifies that register same term for different cultures does not conflict.
    /// </summary>
    [TestMethod]
    public void Register_SameTermForDifferentCultures_DoesNotConflict() {
        var glossary = new MessageGlossary();

        glossary.Register("Jeninnet", _french, "JeninnetFR").Registered.Should().BeTrue();
        var result = glossary.Register("Jeninnet", _german, "JeninnetDE");

        result.Registered.Should().BeTrue("glossaries are per target culture");
        glossary.Terms.Should().HaveCount(2);
    }

    /// <summary>
    /// Verifies that register invalid arguments throw.
    /// </summary>
    [TestMethod]
    public void Register_InvalidArguments_Throw() {
        var glossary = new MessageGlossary();

        var nullSource = () => glossary.Register(null!, _french, "target");
        nullSource.Should().Throw<ArgumentException>();

        var emptySource = () => glossary.Register("  ", _french, "target");
        emptySource.Should().Throw<ArgumentException>();

        var nullCulture = () => glossary.Register("term", null!, "target");
        nullCulture.Should().Throw<ArgumentNullException>();

        var nullTarget = () => glossary.Register("term", _french, null!);
        nullTarget.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that terms returns snapshot unaffected by later registration.
    /// </summary>
    [TestMethod]
    public void Terms_ReturnsSnapshot_UnaffectedByLaterRegistration() {
        var glossary = new MessageGlossary();
        glossary.Register("Jeninnet", _french, "JeninnetFR");

        var snapshot = glossary.Terms;
        glossary.Register("Platform", _french, "PlatformFR");

        snapshot.Should().ContainSingle();
        glossary.Terms.Should().HaveCount(2);
    }
}
