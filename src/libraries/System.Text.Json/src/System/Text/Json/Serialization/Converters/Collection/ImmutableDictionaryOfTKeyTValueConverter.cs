// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text.Json.Serialization.Metadata;

namespace System.Text.Json.Serialization.Converters
{
    internal sealed class ImmutableDictionaryOfTKeyTValueConverter<TCollection, TKey, TValue>
        : DictionaryDefaultConverter<TCollection, TKey, TValue>
        where TCollection : IReadOnlyDictionary<TKey, TValue>
        where TKey : notnull
    {
        protected override void Add(TKey key, in TValue value, JsonSerializerOptions options, ref ReadStack state)
        {
            ((Dictionary<TKey, TValue>)state.Current.ReturnValue!)[key] = value;
        }

        internal override bool CanHaveIdMetadata => false;

        protected override void CreateCollection(ref Utf8JsonReader reader, ref ReadStack state)
        {
            state.Current.ReturnValue = new Dictionary<TKey, TValue>();
        }

        protected override void ConvertCollection(ref ReadStack state, JsonSerializerOptions options)
        {
            JsonTypeInfo typeInfo = state.Current.JsonTypeInfo;

            Func<IEnumerable<KeyValuePair<TKey, TValue>>, TCollection>? creator = (Func<IEnumerable<KeyValuePair<TKey, TValue>>, TCollection>?)typeInfo.CreateObjectWithArgs;
            if (creator == null)
            {
                creator = options.MemberAccessorStrategy.CreateImmutableDictionaryCreateRangeDelegate<TCollection, TKey, TValue>();
                typeInfo.CreateObjectWithArgs = creator;
            }

            state.Current.ReturnValue = creator((Dictionary<TKey, TValue>)state.Current.ReturnValue!);
        }

        protected internal override bool OnWriteResume(Utf8JsonWriter writer, TCollection value, JsonSerializerOptions options, ref WriteStack state)
        {
            IEnumerator<KeyValuePair<TKey, TValue>> enumerator;
            if (state.Current.CollectionEnumerator == null)
            {
                enumerator = value.GetEnumerator();
                if (!enumerator.MoveNext())
                {
                    enumerator.Dispose();
                    return true;
                }
            }
            else
            {
                enumerator = (IEnumerator<KeyValuePair<TKey, TValue>>)state.Current.CollectionEnumerator;
            }

            JsonTypeInfo typeInfo = state.Current.JsonTypeInfo;
            _keyConverter ??= GetConverter<TKey>(typeInfo.KeyTypeInfo!);
            _valueConverter ??= GetConverter<TValue>(typeInfo.ElementTypeInfo!);

            do
            {
                if (ShouldFlush(writer, ref state))
                {
                    state.Current.CollectionEnumerator = enumerator;
                    return false;
                }

                if (state.Current.PropertyState < StackFramePropertyState.Name)
                {
                    state.Current.PropertyState = StackFramePropertyState.Name;

                    TKey key = enumerator.Current.Key;
                    _keyConverter.WriteWithQuotes(writer, key, options, ref state);
                }

                TValue element = enumerator.Current.Value;
                if (!_valueConverter.TryWrite(writer, element, options, ref state))
                {
                    state.Current.CollectionEnumerator = enumerator;
                    return false;
                }

                state.Current.EndDictionaryElement();
            } while (enumerator.MoveNext());

            enumerator.Dispose();
            return true;
        }
    }
}
