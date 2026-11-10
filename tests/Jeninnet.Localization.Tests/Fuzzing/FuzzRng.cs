//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//

namespace Jeninnet.Localization.Tests.Fuzzing;

/// <summary>
/// Deterministic hand-rolled RNG for the fuzz/property suite: xorshift64* with a
/// fixed seed per test area. No framework RNG (whose cross-runtime stability is not
/// contractual), no wall-clock, no new dependencies. The same seed always yields the
/// same sequence on every machine and rebuild.
/// </summary>
internal sealed class FuzzRng {
    private ulong _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="FuzzRng"/> class.
    /// </summary>
    /// <param name="seed">The fixed non-zero seed.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="seed"/> is zero.</exception>
    internal FuzzRng(ulong seed) {
        if(seed == 0) {
            throw new ArgumentException("The fuzz seed must be non-zero.", nameof(seed));
        }

        _state = seed;
    }

    /// <summary>
    /// Draws the next unsigned 64-bit value.
    /// </summary>
    /// <returns>The next value.</returns>
    internal ulong NextUInt64() {
        var x = _state;
        x ^= x >> 12;
        x ^= x << 25;
        x ^= x >> 27;
        _state = x;
        return x * 2685821657736338717ul;
    }

    /// <summary>
    /// Draws a non-negative integer below the exclusive bound.
    /// </summary>
    /// <param name="exclusiveMax">The exclusive upper bound.</param>
    /// <returns>A value in <c>[0, exclusiveMax)</c>.</returns>
    internal int Next(int exclusiveMax) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exclusiveMax);

        return (int)(NextUInt64() % (ulong)exclusiveMax);
    }

    /// <summary>
    /// Draws an integer in the half-open range.
    /// </summary>
    /// <param name="minInclusive">The inclusive lower bound.</param>
    /// <param name="maxExclusive">The exclusive upper bound.</param>
    /// <returns>A value in <c>[minInclusive, maxExclusive)</c>.</returns>
    internal int Next(int minInclusive, int maxExclusive) {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxExclusive, minInclusive);

        return minInclusive + Next(maxExclusive - minInclusive);
    }

    /// <summary>
    /// Draws a boolean.
    /// </summary>
    /// <returns>The drawn value.</returns>
    internal bool NextBool() => (NextUInt64() & 1) == 1;

    /// <summary>
    /// Draws one element from the span.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="choices">The non-empty choices.</param>
    /// <returns>The drawn element.</returns>
    internal T Pick<T>(ReadOnlySpan<T> choices) {
        if(choices.IsEmpty) {
            throw new ArgumentException("Choices must not be empty.", nameof(choices));
        }

        return choices[Next(choices.Length)];
    }

    /// <summary>
    /// Draws a short identifier-like key (1-24 characters, ASCII letters, digits,
    /// dots, dashes, underscores).
    /// </summary>
    /// <returns>The drawn key.</returns>
    internal string NextKey() {
        const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._-";
        var length = Next(1, 25);
        var builder = new StringBuilder(length);
        for(var index = 0; index < length; index++) {
            builder.Append(alphabet[Next(alphabet.Length)]);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Draws a short template from printable text plus format-significant characters.
    /// </summary>
    /// <returns>The drawn template.</returns>
    internal string NextTemplate() {
        const string alphabet = "abcXYZ019 \t{},.:;#'\"\\/=<>!?\u00e9\u4e2d\u263a";
        var length = Next(0, 48);
        var builder = new StringBuilder(length);
        for(var index = 0; index < length; index++) {
            builder.Append(alphabet[Next(alphabet.Length)]);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Draws a hostile token: overlong runs, escapes, quotes, separators, and
    /// non-ASCII bytes-as-text.
    /// </summary>
    /// <returns>The drawn token.</returns>
    internal string NextHostileToken() {
        ReadOnlySpan<string> choices = [
            new string('A', Next(1, 600)),
            new string('=', Next(1, 40)),
            new string('"', Next(1, 12)),
            new string('\'', Next(1, 12)),
            new string('\\', Next(1, 12)),
            new string('\n', Next(1, 6)),
            new string('\r', Next(1, 6)),
            new string('\0', Next(1, 4)),
            "\\\\q\\\\x\\\\u00ZZ\\\\U00110000",
            "\"\"\"",
            "'''",
            "[unclosed",
            "]]]",
            "{{{{",
            "}}}}",
            "{0}{name}{,number}{n, plural, other{#}}",
            "msgctxt msgid msgstr msgstr[0]",
            "#. fuzzy #, fuzzy",
            "\uFEFFbom",
            "مرحبا ünïcödé ✓ \uD800\uDC00",
            "\u0001\u0002\u0003\u0004\u001F\u007F",
        ];
        return Pick(choices);
    }

    /// <summary>
    /// Mutates text with single-point hostile edits: substitution, insertion,
    /// deletion, or truncation.
    /// </summary>
    /// <param name="text">The input text.</param>
    /// <returns>The mutated text.</returns>
    internal string MutateText(string text) {
        ArgumentNullException.ThrowIfNull(text);

        if(text.Length == 0) {
            return NextHostileToken();
        }

        var kind = Next(4);
        var position = Next(text.Length);
        return kind switch {
            0 => string.Concat(text.AsSpan(0, position), NextHostileToken().AsSpan(0, 1), text.AsSpan(position + 1)),
            1 => text.Insert(position, NextHostileToken()),
            2 => text.Remove(position, 1),
            _ => text[..Next(text.Length + 1)],
        };
    }

    /// <summary>
    /// Mutates bytes with single-point edits: substitution, insertion, deletion,
    /// or truncation.
    /// </summary>
    /// <param name="bytes">The input bytes.</param>
    /// <returns>The mutated bytes.</returns>
    internal byte[] MutateBytes(byte[] bytes) {
        ArgumentNullException.ThrowIfNull(bytes);

        if(bytes.Length == 0) {
            return [(byte)Next(256)];
        }

        var kind = Next(4);
        var position = Next(bytes.Length);
        switch(kind) {
            case 0: {
                    var copy = (byte[])bytes.Clone();
                    copy[position] = (byte)Next(256);
                    return copy;
                }

            case 1: {
                    var extra = (byte)Next(256);
                    var grown = new byte[bytes.Length + 1];
                    bytes.AsSpan(0, position).CopyTo(grown);
                    grown[position] = extra;
                    bytes.AsSpan(position).CopyTo(grown.AsSpan(position + 1));
                    return grown;
                }

            case 2: {
                    var shrunk = new byte[bytes.Length - 1];
                    bytes.AsSpan(0, position).CopyTo(shrunk);
                    bytes.AsSpan(position + 1).CopyTo(shrunk.AsSpan(position));
                    return shrunk;
                }

            default:
                return bytes[..Next(bytes.Length + 1)];
        }
    }
}
