using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.Runtime;

namespace MuvluvMod.Services;

using MasterTranslationTables = IReadOnlyDictionary<
    string,
    Dictionary<string, Dictionary<string, string>>
>;

/// <summary>
/// Summarizes how many MasterData objects matched a translation type and how many fields changed.
/// </summary>
public readonly struct MasterDataTranslationResult
{
    public int MatchedObjects { get; }
    public int TranslatedFields { get; }

    public MasterDataTranslationResult(int matchedObjects, int translatedFields)
    {
        MatchedObjects = matchedObjects;
        TranslatedFields = translatedFields;
    }
}

/// <summary>
/// Applies class/property translation tables to loaded MasterData objects.
/// Property paths use <c>::</c> for nesting and <c>[]</c> for collection traversal.
/// Examples: <c>Name</c>, <c>Wrapper::Text</c>, and <c>Items[]::Text</c>.
/// </summary>
public sealed class MasterDataTranslator
{
    private readonly ConcurrentDictionary<Type, TranslationPlan> _plans = new();
    private readonly ConcurrentDictionary<IntPtr, RuntimeTypeBinding> _runtimeBindings = new();
    private readonly ConcurrentDictionary<string, byte> _loggedRuntimeErrors = new();

    private static readonly MethodInfo CreateWrapperFactoryMethod =
        typeof(MasterDataTranslator).GetMethod(
            nameof(CreateWrapperFactory),
            BindingFlags.NonPublic | BindingFlags.Static
        );

    public MasterDataTranslationResult Translate(
        IEnumerable objects,
        MasterTranslationTables translationTables
    )
    {
        if (objects == null || translationTables == null || translationTables.Count == 0)
            return new MasterDataTranslationResult(0, 0);

        int matchedObjectCount = 0;
        int translatedCount = 0;
        foreach (var obj in objects)
        {
            if (obj == null)
                continue;

            if (!TryResolveTarget(obj, translationTables, out var target, out var plan))
                continue;

            matchedObjectCount++;
            foreach (var entry in plan.Entries)
            {
                try
                {
                    translatedCount += entry.Path.Translate(target, entry.Translations);
                }
                catch (Exception e)
                {
                    LogRuntimeErrorOnce(plan.Type, entry.PathText, e);
                }
            }
        }

        return new MasterDataTranslationResult(matchedObjectCount, translatedCount);
    }

    private bool TryResolveTarget(
        object obj,
        MasterTranslationTables translationTables,
        out object target,
        out TranslationPlan plan
    )
    {
        if (obj is not Il2CppObjectBase il2CppObject)
        {
            target = obj;
            var type = obj.GetType();
            if (!translationTables.TryGetValue(type.Name, out var propertyTables))
            {
                plan = null;
                return false;
            }

            if (!_plans.TryGetValue(type, out plan))
            {
                var newPlan = TranslationPlan.Create(type, propertyTables);
                plan = _plans.GetOrAdd(type, newPlan);
            }
            return true;
        }

        var binding = GetRuntimeBinding(il2CppObject, translationTables);
        if (!binding.Valid)
        {
            target = null;
            plan = null;
            return false;
        }

        target = binding.Wrap(il2CppObject.Pointer);
        plan = binding.Plan;
        return target != null;
    }

    private RuntimeTypeBinding GetRuntimeBinding(
        Il2CppObjectBase obj,
        MasterTranslationTables translationTables
    )
    {
        IntPtr classPointer = obj.ObjectClass;
        if (classPointer == IntPtr.Zero)
            return RuntimeTypeBinding.Ignored;

        if (_runtimeBindings.TryGetValue(classPointer, out var binding))
            return binding;

        var newBinding = CreateRuntimeBinding(
            obj.GetType().Assembly,
            classPointer,
            translationTables
        );
        if (_runtimeBindings.TryAdd(classPointer, newBinding))
            return newBinding;

        return _runtimeBindings[classPointer];
    }

    private static RuntimeTypeBinding CreateRuntimeBinding(
        Assembly interopAssembly,
        IntPtr classPointer,
        MasterTranslationTables translationTables
    )
    {
        string className = IL2CPP.il2cpp_class_get_name_(classPointer);
        if (
            string.IsNullOrEmpty(className)
            || !translationTables.TryGetValue(className, out var propertyTables)
        )
            return RuntimeTypeBinding.Ignored;

        string classNamespace = IL2CPP.il2cpp_class_get_namespace_(classPointer);
        string fullName = string.IsNullOrEmpty(classNamespace)
            ? className
            : $"{classNamespace}.{className}";
        Type managedType = interopAssembly.GetType(fullName);
        if (managedType == null || !typeof(Il2CppObjectBase).IsAssignableFrom(managedType))
        {
            Logger.Warn($"MasterData wrapper type not found: {fullName}");
            return RuntimeTypeBinding.Ignored;
        }

        try
        {
            var factory =
                (Func<IntPtr, object>)
                    CreateWrapperFactoryMethod.MakeGenericMethod(managedType).Invoke(null, null);
            return new RuntimeTypeBinding(
                TranslationPlan.Create(managedType, propertyTables),
                factory
            );
        }
        catch (Exception e)
        {
            Logger.Warn(
                $"MasterData wrapper factory failed [{fullName}]: {e.GetBaseException().Message}"
            );
            return RuntimeTypeBinding.Ignored;
        }
    }

    private static Func<IntPtr, object> CreateWrapperFactory<T>()
        where T : Il2CppObjectBase => pointer => Il2CppObjectPool.Get<T>(pointer);

    private void LogRuntimeErrorOnce(Type type, string path, Exception exception)
    {
        string key = $"{type.FullName}\0{path}\0{exception.GetType().FullName}";
        if (_loggedRuntimeErrors.TryAdd(key, 0))
            Logger.Warn(
                $"MasterData path failed [{type.Name}.{path}]: {exception.GetBaseException().Message}"
            );
    }

    private sealed class CompiledPropertyPath
    {
        private const string Separator = "::";
        private const string CollectionSuffix = "[]";

        private readonly PathSegment[] _segments;

        public bool Valid => _segments != null;

        private CompiledPropertyPath(PathSegment[] segments)
        {
            _segments = segments;
        }

        public static CompiledPropertyPath Create(Type rootType, string path)
        {
            if (rootType == null || string.IsNullOrWhiteSpace(path))
                return Invalid(rootType, path, "path is empty");

            string[] tokens = path.Split(new[] { Separator }, StringSplitOptions.None);
            var segments = new PathSegment[tokens.Length];
            Type currentType = rootType;

            for (int index = 0; index < tokens.Length; index++)
            {
                string token = tokens[index].Trim();
                bool isCollection = token.EndsWith(CollectionSuffix, StringComparison.Ordinal);
                string propertyName = isCollection ? token[..^CollectionSuffix.Length] : token;

                if (string.IsNullOrWhiteSpace(propertyName))
                    return Invalid(rootType, path, "property name is empty");

                var property = currentType.GetProperty(
                    propertyName,
                    BindingFlags.Instance | BindingFlags.Public
                );
                if (
                    property == null
                    || !property.CanRead
                    || property.GetIndexParameters().Length != 0
                )
                    return Invalid(
                        rootType,
                        path,
                        $"property {currentType.Name}.{propertyName} is not readable"
                    );

                bool isLast = index == tokens.Length - 1;
                if (isLast)
                {
                    if (isCollection)
                        return Invalid(rootType, path, "the final property cannot be a collection");
                    if (property.PropertyType != typeof(string) || !property.CanWrite)
                        return Invalid(
                            rootType,
                            path,
                            $"final property {currentType.Name}.{propertyName} is not a writable string"
                        );
                }
                else if (isCollection)
                {
                    currentType = GetCollectionElementType(property.PropertyType);
                    if (currentType == null)
                        return Invalid(
                            rootType,
                            path,
                            $"property {property.DeclaringType?.Name}.{propertyName} has no collection element type"
                        );
                }
                else
                {
                    currentType = property.PropertyType;
                }

                segments[index] = new PathSegment(property, isCollection);
            }

            return new CompiledPropertyPath(segments);
        }

        public int Translate(object root, IReadOnlyDictionary<string, string> translations) =>
            TranslateAt(root, 0, translations);

        private int TranslateAt(
            object current,
            int segmentIndex,
            IReadOnlyDictionary<string, string> translations
        )
        {
            if (current == null)
                return 0;

            var segment = _segments[segmentIndex];
            object value = segment.Property.GetValue(current);
            bool isLast = segmentIndex == _segments.Length - 1;

            if (isLast)
            {
                if (
                    value is string original
                    && translations.TryGetValue(original, out string translated)
                    && !string.Equals(original, translated, StringComparison.Ordinal)
                )
                {
                    segment.Property.SetValue(current, translated);
                    return 1;
                }

                return 0;
            }

            if (!segment.IsCollection)
                return TranslateAt(value, segmentIndex + 1, translations);

            if (value is not IEnumerable items)
                return 0;

            int translatedCount = 0;
            foreach (var item in items)
                translatedCount += TranslateAt(item, segmentIndex + 1, translations);

            return translatedCount;
        }

        private static Type GetCollectionElementType(Type collectionType)
        {
            if (collectionType.IsArray)
                return collectionType.GetElementType();

            if (collectionType.IsGenericType)
            {
                var arguments = collectionType.GetGenericArguments();
                if (arguments.Length == 1)
                    return arguments[0];
            }

            foreach (var implementedInterface in collectionType.GetInterfaces())
            {
                if (
                    implementedInterface.IsGenericType
                    && implementedInterface.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                )
                    return implementedInterface.GetGenericArguments()[0];
            }

            return null;
        }

        private static CompiledPropertyPath Invalid(Type type, string path, string reason)
        {
            Logger.Warn($"Invalid MasterData path [{type?.Name}.{path}]: {reason}");
            return new CompiledPropertyPath(null);
        }
    }

    private readonly struct PathSegment
    {
        public PropertyInfo Property { get; }
        public bool IsCollection { get; }

        public PathSegment(PropertyInfo property, bool isCollection)
        {
            Property = property;
            IsCollection = isCollection;
        }
    }

    private sealed class TranslationPlan
    {
        public Type Type { get; }
        public TranslationEntry[] Entries { get; }

        private TranslationPlan(Type type, TranslationEntry[] entries)
        {
            Type = type;
            Entries = entries;
        }

        public static TranslationPlan Create(
            Type type,
            IReadOnlyDictionary<string, Dictionary<string, string>> propertyTables
        )
        {
            var entries = new List<TranslationEntry>(propertyTables.Count);
            foreach (var (pathText, translations) in propertyTables)
            {
                if (translations == null || translations.Count == 0)
                    continue;

                var path = CompiledPropertyPath.Create(type, pathText);
                if (path.Valid)
                    entries.Add(new TranslationEntry(pathText, path, translations));
            }

            return new TranslationPlan(type, entries.ToArray());
        }
    }

    private readonly struct TranslationEntry
    {
        public string PathText { get; }
        public CompiledPropertyPath Path { get; }
        public IReadOnlyDictionary<string, string> Translations { get; }

        public TranslationEntry(
            string pathText,
            CompiledPropertyPath path,
            IReadOnlyDictionary<string, string> translations
        )
        {
            PathText = pathText;
            Path = path;
            Translations = translations;
        }
    }

    private sealed class RuntimeTypeBinding
    {
        public static RuntimeTypeBinding Ignored { get; } = new(null, null);

        public TranslationPlan Plan { get; }
        public Func<IntPtr, object> Wrap { get; }
        public bool Valid => Plan != null && Wrap != null;

        public RuntimeTypeBinding(TranslationPlan plan, Func<IntPtr, object> wrap)
        {
            Plan = plan;
            Wrap = wrap;
        }
    }
}
