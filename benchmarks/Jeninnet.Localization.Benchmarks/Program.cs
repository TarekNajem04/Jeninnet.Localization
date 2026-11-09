//
// Copyright (c) Jeninnet.
// Part of the Jeninnet Platform.
// Repository: https://github.com/TarekNajem04/Jeninnet.Localization
// Licensed under the MIT License.
//
using System.Reflection;
using BenchmarkDotNet.Running;

var switcher = new BenchmarkSwitcher(Assembly.GetExecutingAssembly());
switcher.Run(args);
