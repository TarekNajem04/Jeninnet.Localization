//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Tests;

/// <summary>
/// Focused contract evidence for <see cref="LocalizedStringEntry"/>: construction validation,
/// the missing-resource placeholder and its distinction from a valid empty value, identity by id,
/// rendering rules, and the null-safe implicit string conversion.
/// </summary>
[TestClass]
public class LocalizedStringEntryContractTests {
    /// <summary>
    /// Verifies that ctor populates all properties and is not missing.
    /// </summary>
    [TestMethod]
    public void Ctor_PopulatesAllPropertiesAndIsNotMissing() {
        var id = Guid.NewGuid();
        var entry = new LocalizedStringEntry(id, "TestSet", "en-US", "Key", "Value");

        entry.Id.Should().Be(id);
        entry.ResourceSet.Should().Be("TestSet");
        entry.Culture.Should().Be("en-US");
        entry.Name.Should().Be("Key");
        entry.Value.Should().Be("Value");
        entry.ResourceNotFound.Should().BeFalse();
        entry.IsMissing().Should().BeFalse();
    }

    /// <summary>
    /// Verifies that ctor with resource not found is missing.
    /// </summary>
    [TestMethod]
    public void Ctor_WithResourceNotFound_IsMissing() {
        var entry = new LocalizedStringEntry(Guid.NewGuid(), "TestSet", "en-US", "Key", "Value", resourceNotFound: true);

        entry.ResourceNotFound.Should().BeTrue();
        entry.IsMissing().Should().BeTrue();
    }

    /// <summary>
    /// Verifies that ctor null arguments throw argument null exception.
    /// </summary>
    [TestMethod]
    public void Ctor_NullArguments_ThrowArgumentNullException() {
        var id = Guid.NewGuid();

        var actResourceSet = () => new LocalizedStringEntry(id, null!, "en-US", "Key", "Value");
        var actCulture = () => new LocalizedStringEntry(id, "TestSet", null!, "Key", "Value");
        var actName = () => new LocalizedStringEntry(id, "TestSet", "en-US", null!, "Value");
        var actValue = () => new LocalizedStringEntry(id, "TestSet", "en-US", "Key", null!);

        actResourceSet.Should().Throw<ArgumentNullException>();
        actCulture.Should().Throw<ArgumentNullException>();
        actName.Should().Throw<ArgumentNullException>();
        actValue.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that missing creates placeholder entry with empty id and resource not found.
    /// </summary>
    [TestMethod]
    public void Missing_CreatesPlaceholderEntryWithEmptyIdAndResourceNotFound() {
        var entry = LocalizedStringEntry.Missing("TestSet", "en-US", "MissingKey");

        entry.Id.Should().Be(Guid.Empty);
        entry.ResourceSet.Should().Be("TestSet");
        entry.Culture.Should().Be("en-US");
        entry.Name.Should().Be("MissingKey");
        entry.Value.Should().Be("[MISSING:MissingKey]");
        entry.ResourceNotFound.Should().BeTrue();
        entry.IsMissing().Should().BeTrue();
    }

    /// <summary>
    /// Verifies that missing is distinct from valid empty string value.
    /// </summary>
    [TestMethod]
    public void Missing_IsDistinctFromValidEmptyStringValue() {
        var validEmpty = new LocalizedStringEntry(Guid.NewGuid(), "TestSet", "en-US", "Empty", "");
        var missing = LocalizedStringEntry.Missing("TestSet", "en-US", "Empty");

        validEmpty.IsMissing().Should().BeFalse();
        validEmpty.Value.Should().Be(string.Empty, "an empty string is a valid stored value");
        missing.IsMissing().Should().BeTrue();
        missing.Value.Should().Be("[MISSING:Empty]", "the missing placeholder is never a valid empty value");
    }

    /// <summary>
    /// Verifies that to string returns value when found.
    /// </summary>
    [TestMethod]
    public void ToString_ReturnsValueWhenFound() {
        var entry = new LocalizedStringEntry(Guid.NewGuid(), "TestSet", "en-US", "Key", "Value");

        entry.ToString().Should().Be("Value");
    }

    /// <summary>
    /// Verifies that to string prefixes missing marker when not found.
    /// </summary>
    [TestMethod]
    public void ToString_PrefixesMissingMarkerWhenNotFound() {
        var entry = new LocalizedStringEntry(Guid.NewGuid(), "TestSet", "en-US", "Key", "generic-value", resourceNotFound: true);

        entry.ToString().Should().Be("[MISSING] generic-value");
    }

    /// <summary>
    /// Verifies that to string does not double prefix when value already contains missing.
    /// </summary>
    [TestMethod]
    public void ToString_DoesNotDoublePrefixWhenValueAlreadyContainsMissing() {
        var entry = LocalizedStringEntry.Missing("TestSet", "en-US", "Key");

        entry.ToString().Should().Be("[MISSING:Key]");
    }

    /// <summary>
    /// Verifies that implicit conversion to nullable string returns value.
    /// </summary>
    [TestMethod]
    public void ImplicitConversion_ToNullableString_ReturnsValue() {
        string? converted = new LocalizedStringEntry(Guid.NewGuid(), "TestSet", "en-US", "Key", "Value");

        converted.Should().Be("Value");
    }

    /// <summary>
    /// Verifies that implicit conversion null entry returns null.
    /// </summary>
    [TestMethod]
    public void ImplicitConversion_NullEntry_ReturnsNull() {
        LocalizedStringEntry? entry = null;

        string? converted = entry!;

        converted.Should().BeNull();
    }

    /// <summary>
    /// Verifies that equality is by id only.
    /// </summary>
    [TestMethod]
    public void Equality_IsByIdOnly() {
        var id = Guid.NewGuid();
        var first = new LocalizedStringEntry(id, "TestSet", "en-US", "Key", "Value");
        var sameIdentity = new LocalizedStringEntry(id, "OtherSet", "fr-FR", "Other", "different-value");

        first.Equals(sameIdentity).Should().BeTrue("identity is defined by the entry id alone");
        first.GetHashCode().Should().Be(sameIdentity.GetHashCode());

        var otherIdentity = new LocalizedStringEntry(Guid.NewGuid(), "TestSet", "en-US", "Key", "Value");
        first.Equals(otherIdentity).Should().BeFalse();
        first.Equals(null).Should().BeFalse();
    }

    /// <summary>
    /// Verifies that value is mutable after construction.
    /// </summary>
    [TestMethod]
    public void Value_IsMutableAfterConstruction() {
        var entry = new LocalizedStringEntry(Guid.NewGuid(), "TestSet", "en-US", "Key", "Before");

        entry.Value.Should().Be("Before");

        entry.Value = "After";

        entry.Value.Should().Be("After");
    }
}
