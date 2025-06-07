#if NET8_0_OR_GREATER

using System;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace SwiftCollections
{
    public class SwiftStackJsonConverter<T> : JsonConverter<SwiftStack<T>>
    {
        public override SwiftStack<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Read as array
            var items = JsonSerializer.Deserialize<T[]>(ref reader, options);
            // Push in order so that stack is correct
            var stack = new SwiftStack<T>();
            foreach (var item in items)
            {
                stack.Push(item);
            }
            return stack;
        }

        public override void Write(Utf8JsonWriter writer, SwiftStack<T> value, JsonSerializerOptions options)
        {
            // Serialize Push order → array[0.._count-1]
            var items = new T[value.Count];
            value.CopyTo(items, 0);
            JsonSerializer.Serialize(writer, items, options);
        }
    }
}

#endif