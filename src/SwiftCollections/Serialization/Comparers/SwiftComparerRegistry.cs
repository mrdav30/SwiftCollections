//=======================================================================
// SwiftComparerRegistry.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Chronicler.Diagnostics;
using SwiftCollections.Utility;

namespace SwiftCollections.Serialization;

/// <summary>Resolves typed ordering and equality/hash rules at collection persistence boundaries.</summary>
/// <remarks>
/// Definitions are immutable and may be shared across serializer operations. Factory-created immutable
/// snapshots receive weak, registry-local recapture bindings. Callers must synchronize source collection and
/// comparer settings during capture. This registry does not prove application comparer determinism, retain
/// mutable configuration buffers, or participate in ordinary collection comparisons.
/// </remarks>
public sealed class SwiftComparerRegistry : IServiceProvider
{
    internal const int MaxIdLength = 128;
    internal const int MaxConfigurationLength = 4096;
    internal const ushort BuiltInVersion = 1;
    private const string DefaultOrderingId = "lsf.runtime-ordering-default";
    private const string DefaultEqualityId = "lsf.runtime-equality-default";
    private const string SeededStringId = "lsf.swift-string-seeded";
    private const string SeededObjectId = "lsf.swift-object-seeded";
    private const string RandomizedStringId = "lsf.swift-string-randomized";
    private const string RandomizedObjectId = "lsf.swift-object-randomized";
    private const string OrdinalId = "lsf.string-ordinal";
    private const string OrdinalIgnoreCaseId = "lsf.string-ordinal-ignore-case";

    private readonly SwiftDictionary<(Type Type, string Id, ushort Version), object> _codecs;
    private readonly SwiftDictionary<Type, SwiftDictionary<object, object>> _sources = new();
    private readonly SwiftDictionary<Type, object> _produced = new();
    private readonly object _producedLock = new();

    /// <summary>Gets the registry containing supported built-in rules and no application registrations.</summary>
    /// <value>The shared immutable built-ins-only registry.</value>
    public static SwiftComparerRegistry Empty { get; } = new SwiftComparerRegistryBuilder().Build();

    internal SwiftComparerRegistry(SwiftDictionary<(Type Type, string Id, ushort Version), object> codecs,
        SwiftDictionary<Type, SwiftDictionary<object, object>> sources)
    {
        _codecs = new SwiftDictionary<(Type Type, string Id, ushort Version), object>(codecs);
        foreach (KeyValuePair<Type, SwiftDictionary<object, object>> pair in sources)
            _sources.Add(pair.Key, new SwiftDictionary<object, object>(pair.Value, pair.Value.Comparer));
    }

    /// <summary>Provides this registry to a serializer service-provider lookup.</summary>
    /// <param name="serviceType">The requested service type; must be non-null.</param>
    /// <returns>This instance for SwiftComparerRegistry; null for other services. Existing host services must
    /// be composed by the host rather than replaced.</returns>
    /// <exception cref="ArgumentNullException">serviceType is null.</exception>
    public object? GetService(Type serviceType)
    {
        ChronicleGuard.ThrowIfNull(serviceType, nameof(serviceType));
        return serviceType == typeof(SwiftComparerRegistry) ? this : null;
    }

    internal SwiftComparerDescriptor CaptureOrdering<T>(IComparer<T> comparer)
    {
        ChronicleGuard.ThrowIfNull(comparer, nameof(comparer));
        if (TryCaptureBuiltInOrdering(comparer, out SwiftComparerDescriptor builtIn))
            return builtIn;
        return CaptureCustom(comparer);
    }

    internal SwiftComparerDescriptor CaptureEquality<T>(IEqualityComparer<T> comparer)
    {
        ChronicleGuard.ThrowIfNull(comparer, nameof(comparer));
        if (TryCaptureBuiltInEquality(comparer, out SwiftComparerDescriptor builtIn))
            return builtIn;
        return CaptureCustom(comparer);
    }

    private SwiftComparerDescriptor CaptureCustom<TComparer>(TComparer comparer) where TComparer : class
    {
        if (TryGetSource(comparer, out SwiftComparerRegistration<TComparer> registration))
        {
            ReadOnlyMemory<byte> config = registration.Encode(comparer);
            if (config.Length > MaxConfigurationLength)
                throw new NotSupportedException("The comparer codec configuration exceeds 4,096 bytes.");
            return new SwiftComparerDescriptor(registration.Id, registration.Version, config.ToArray());
        }

        lock (_producedLock)
        {
            if (_produced.TryGetValue(typeof(TComparer), out object? table)
                && ((ConditionalWeakTable<TComparer, ComparerBinding>)table).TryGetValue(comparer,
                    out ComparerBinding? binding))
                return binding.Descriptor;
        }
        throw new NotSupportedException(
            "This comparer has no supported persistence rule. Register its exact typed instance and codec.");
    }

    internal IComparer<T> RestoreOrdering<T>(SwiftComparerDescriptor descriptor)
    {
        try
        {
            if (descriptor.Id.StartsWith("lsf.", StringComparison.Ordinal))
                return RestoreBuiltInOrdering<T>(descriptor);
            return RestoreCustom<IComparer<T>>(descriptor, static comparer => TryCaptureBuiltInOrdering(comparer, out _));
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("The ordering comparer could not be restored.", exception);
        }
    }

    internal IEqualityComparer<T> RestoreEquality<T>(SwiftComparerDescriptor descriptor)
    {
        try
        {
            if (descriptor.Id.StartsWith("lsf.", StringComparison.Ordinal))
                return RestoreBuiltInEquality<T>(descriptor);
            return RestoreCustom<IEqualityComparer<T>>(descriptor, static comparer => TryCaptureBuiltInEquality(comparer, out _));
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("The equality comparer could not be restored.", exception);
        }
    }

    private TComparer RestoreCustom<TComparer>(SwiftComparerDescriptor descriptor, Func<TComparer, bool> isBuiltIn)
        where TComparer : class
    {
        if (!_codecs.TryGetValue((typeof(TComparer), descriptor.Id, descriptor.Version), out object? codec))
            throw new InvalidOperationException("The typed comparer codec identifier/version is not registered.");
        var registration = (SwiftComparerRegistration<TComparer>)codec;
        byte[] config = descriptor.Config.Length == 0 ? Array.Empty<byte>() : (byte[])descriptor.Config.Clone();
        (TComparer comparer, int consumed) = registration.Decode(config);
        if (comparer == null || consumed != config.Length)
            throw new InvalidOperationException("The comparer factory must return a comparer and consume all configuration bytes.");
        if (isBuiltIn(comparer))
            throw new InvalidOperationException("An application factory cannot alias a supported built-in comparer.");

        if (TryGetSource(comparer, out SwiftComparerRegistration<TComparer> source))
        {
            ReadOnlyMemory<byte> sourceConfig = source.Encode(comparer);
            if (source.Id != descriptor.Id || source.Version != descriptor.Version
                || !sourceConfig.Span.SequenceEqual(descriptor.Config))
                throw new InvalidOperationException("The factory returned a singleton with a conflicting comparer binding.");
            return comparer;
        }

        // Wire readers own descriptor storage; only the factory receives a separate copy.
        lock (_producedLock)
        {
            if (!_produced.TryGetValue(typeof(TComparer), out object? table))
            {
                table = new ConditionalWeakTable<TComparer, ComparerBinding>();
                _produced.Add(typeof(TComparer), table);
            }
            var bindings = (ConditionalWeakTable<TComparer, ComparerBinding>)table;
            if (bindings.TryGetValue(comparer, out ComparerBinding? binding))
            {
                SwiftComparerDescriptor existing = binding.Descriptor;
                if (existing.Id != descriptor.Id || existing.Version != descriptor.Version
                    || !existing.Config.AsSpan().SequenceEqual(descriptor.Config))
                    throw new InvalidOperationException("The factory returned a comparer with a conflicting binding.");
            }
            else
                bindings.Add(comparer, new ComparerBinding(descriptor));
        }
        return comparer;
    }

    internal static void ValidateId(string id)
    {
        ChronicleGuard.ThrowIfNull(id, nameof(id));
        ChronicleGuard.ThrowIfArgument(id.Length == 0 || id.Length > MaxIdLength, nameof(id),
            "A comparer identifier must contain 1–128 lowercase ASCII characters.");
        foreach (char character in id)
            if (!(character >= 'a' && character <= 'z') && !(character >= '0' && character <= '9')
                && character != '.' && character != '_' && character != '-' && character != '/')
                throw new ArgumentException("The comparer identifier contains an unsupported character.", nameof(id));
    }

    internal static bool TryCaptureBuiltInOrdering<T>(IComparer<T> comparer, out SwiftComparerDescriptor descriptor)
    {
        string? id = null;
        if (DefaultOrdering<T>.Eligible && ReferenceEquals(comparer, Comparer<T>.Default))
            id = DefaultOrderingId;
        else if (typeof(T) == typeof(string))
        {
            if (ReferenceEquals(comparer, StringComparer.Ordinal)) id = OrdinalId;
            else if (ReferenceEquals(comparer, StringComparer.OrdinalIgnoreCase)) id = OrdinalIgnoreCaseId;
        }
        descriptor = new SwiftComparerDescriptor(id!, BuiltInVersion, Array.Empty<byte>());
        return id != null;
    }

    private bool TryGetSource<TComparer>(TComparer comparer, out SwiftComparerRegistration<TComparer> registration)
        where TComparer : class
    {
        if (_sources.TryGetValue(typeof(TComparer), out SwiftDictionary<object, object>? instances)
            && instances.TryGetValue(comparer, out object? value))
        {
            registration = (SwiftComparerRegistration<TComparer>)value;
            return true;
        }
        registration = null!;
        return false;
    }

    private static IComparer<T> RestoreBuiltInOrdering<T>(SwiftComparerDescriptor descriptor)
    {
        if (descriptor.Version != BuiltInVersion || descriptor.Config.Length != 0)
            throw new InvalidOperationException("The built-in ordering codec version/configuration is unsupported.");
        if (descriptor.Id == DefaultOrderingId && DefaultOrdering<T>.Eligible)
            return Comparer<T>.Default;
        if (typeof(T) == typeof(string))
        {
            if (descriptor.Id == OrdinalId) return (IComparer<T>)(object)StringComparer.Ordinal;
            if (descriptor.Id == OrdinalIgnoreCaseId) return (IComparer<T>)(object)StringComparer.OrdinalIgnoreCase;
        }
        throw new InvalidOperationException("The built-in ordering rule is unsupported for this declared element type.");
    }

    internal static bool TryCaptureBuiltInEquality<T>(IEqualityComparer<T> comparer, out SwiftComparerDescriptor descriptor)
    {
        string? id = null;
        byte[] configuration = Array.Empty<byte>();
        if (ReferenceEquals(comparer, EqualityComparer<T>.Default))
            id = DefaultEqualityId;
        else if (comparer is SwiftDeterministicObjectEqualityComparer seededObject && !typeof(T).IsValueType)
        {
            id = SeededObjectId;
            configuration = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(configuration, seededObject.Seed);
        }
        else if (comparer is SwiftObjectEqualityComparer randomizedObject && !typeof(T).IsValueType)
        {
            id = RandomizedObjectId;
            configuration = new byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(configuration, randomizedObject.Entropy);
        }
        else if (typeof(T) == typeof(string))
        {
            if (ReferenceEquals(comparer, StringComparer.Ordinal)) id = OrdinalId;
            else if (ReferenceEquals(comparer, StringComparer.OrdinalIgnoreCase)) id = OrdinalIgnoreCaseId;
            else if (comparer is SwiftDeterministicStringEqualityComparer seeded)
            {
                id = SeededStringId;
                configuration = new byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(configuration, seeded.Seed);
            }
            else if (comparer is SwiftStringEqualityComparer randomized)
            {
                id = RandomizedStringId;
                configuration = new byte[8];
                BinaryPrimitives.WriteInt64LittleEndian(configuration, randomized.Entropy);
            }
        }
        descriptor = new SwiftComparerDescriptor(id!, BuiltInVersion, configuration);
        return id != null;
    }

    private static IEqualityComparer<T> RestoreBuiltInEquality<T>(SwiftComparerDescriptor descriptor)
    {
        if (descriptor.Version != BuiltInVersion)
            throw new InvalidOperationException("The built-in equality codec version is unsupported.");
        if (descriptor.Id == DefaultEqualityId && descriptor.Config.Length == 0)
            return EqualityComparer<T>.Default;
        if (typeof(T) == typeof(string))
        {
            if (descriptor.Id == OrdinalId && descriptor.Config.Length == 0)
                return (IEqualityComparer<T>)(object)StringComparer.Ordinal;
            if (descriptor.Id == OrdinalIgnoreCaseId && descriptor.Config.Length == 0)
                return (IEqualityComparer<T>)(object)StringComparer.OrdinalIgnoreCase;
            if (descriptor.Id == SeededStringId && descriptor.Config.Length == 4)
                return (IEqualityComparer<T>)(object)SwiftHashTools.GetDeterministicStringEqualityComparer(
                    BinaryPrimitives.ReadInt32LittleEndian(descriptor.Config));
            if (descriptor.Id == RandomizedStringId && descriptor.Config.Length == 8)
                return (IEqualityComparer<T>)(object)new SwiftStringEqualityComparer(
                    BinaryPrimitives.ReadInt64LittleEndian(descriptor.Config));
        }
        if (!typeof(T).IsValueType)
        {
            if (descriptor.Id == SeededObjectId && descriptor.Config.Length == 4)
                return (IEqualityComparer<T>)(object)SwiftHashTools.GetDeterministicObjectEqualityComparer(
                    BinaryPrimitives.ReadInt32LittleEndian(descriptor.Config));
            if (descriptor.Id == RandomizedObjectId && descriptor.Config.Length == 8)
                return (IEqualityComparer<T>)(object)new SwiftObjectEqualityComparer(
                    BinaryPrimitives.ReadInt64LittleEndian(descriptor.Config));
        }
        throw new InvalidOperationException("The built-in equality rule or configuration is unsupported for this declared type.");
    }

    internal static void ValidateReverseEquality<T>(SwiftComparerDescriptor configured, SwiftComparerDescriptor effective)
    {
        if (configured.Id == effective.Id && configured.Version == effective.Version
            && configured.Config.AsSpan().SequenceEqual(effective.Config))
            return;

        // Ordinary collision hardening preserves equality while replacing only these hashing policies.
        if (configured.Version == BuiltInVersion && effective.Version == BuiltInVersion
            && effective.Config.Length == 8)
        {
            bool runtimeDefault = configured.Id == DefaultEqualityId && configured.Config.Length == 0;
            if (typeof(T) == typeof(string) && effective.Id == RandomizedStringId
                && (runtimeDefault || configured.Id == SeededStringId && configured.Config.Length == 4))
                return;
            if (!typeof(T).IsValueType && effective.Id == RandomizedObjectId
                && (runtimeDefault && typeof(T) == typeof(object)
                    || configured.Id == SeededObjectId && configured.Config.Length == 4))
                return;
        }
        throw new InvalidOperationException("Configured and effective reverse comparison rules are contradictory.");
    }

    private sealed class ComparerBinding
    {
        internal readonly SwiftComparerDescriptor Descriptor;

        internal ComparerBinding(SwiftComparerDescriptor descriptor) => Descriptor = descriptor;
    }

    private static class DefaultOrdering<T>
    {
        internal static readonly bool Eligible = IsEligible();

        private static bool IsEligible()
        {
            Type type = typeof(T);
            if (type == typeof(string) || type == typeof(object)) return false;
            Type? underlying = Nullable.GetUnderlyingType(type);
            if (underlying == null)
                return type.IsEnum || typeof(IComparable<T>).IsAssignableFrom(type)
                    || typeof(IComparable).IsAssignableFrom(type);
            if (underlying.IsEnum || typeof(IComparable).IsAssignableFrom(underlying)) return true;
            foreach (Type contract in underlying.GetInterfaces())
                if (contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IComparable<>)
                    && contract.GetGenericArguments()[0] == underlying)
                    return true;
            return false;
        }
    }
}
