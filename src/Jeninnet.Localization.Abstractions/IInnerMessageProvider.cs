//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
namespace Jeninnet.Localization.Abstractions;

/// <summary>
/// Marker interface implemented by the localization message providers that Core's
/// exactly-one provider selector resolves (the exactly-one-provider rule).
/// </summary>
public interface IInnerMessageProvider : IMessageProvider;
