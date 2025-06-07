#if NET8_0_OR_GREATER

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwiftCollections.Dimensions
{
    public class Array3DJsonConverter : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            // Walk base types to find Array3D<T>
            while (typeToConvert != null && typeToConvert != typeof(object))
            {
                if (typeToConvert.IsGenericType &&
                    typeToConvert.GetGenericTypeDefinition() == typeof(Array3D<>))
                {
                    return true;
                }

                typeToConvert = typeToConvert.BaseType;
            }

            return false;
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            // Find the generic argument T
            Type current = typeToConvert;
            while (current != null && current != typeof(object))
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(Array3D<>))
                {
                    Type itemType = current.GetGenericArguments()[0];
                    Type bridgeConverterType = typeof(Array3DJsonConverterBridge<,>).MakeGenericType(typeToConvert, itemType);
                    return (JsonConverter)Activator.CreateInstance(bridgeConverterType);
                }

                current = current.BaseType;
            }

            throw new InvalidOperationException("Array3DJsonConverterFactory: Unable to create converter for " + typeToConvert);
        }

        // Bridge to support subclasses
        private class Array3DJsonConverterBridge<TTarget, T> : JsonConverter<TTarget> where TTarget : Array3D<T>
        {
            public override TTarget Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var doc = JsonDocument.ParseValue(ref reader);
                int width = doc.RootElement.GetProperty("Width").GetInt32();
                int height = doc.RootElement.GetProperty("Height").GetInt32();
                int length = doc.RootElement.GetProperty("Length").GetInt32();

                T[] data = JsonSerializer.Deserialize<T[]>(
                    doc.RootElement.GetProperty("Data").GetRawText(), options);

                var result = (TTarget)Activator.CreateInstance(typeof(TTarget), width, height, length);

                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        for (int z = 0; z < length; z++)
                        {
                            int index = x * (height * length) + y * length + z;
                            result[x, y, z] = data[index];
                        }
                    }
                }

                return result;
            }

            public override void Write(Utf8JsonWriter writer, TTarget value, JsonSerializerOptions options)
            {
                T[] flatData = new T[value.Width * value.Height * value.Length];

                for (int x = 0; x < value.Width; x++)
                {
                    for (int y = 0; y < value.Height; y++)
                    {
                        for (int z = 0; z < value.Length; z++)
                        {
                            int index = x * (value.Height * value.Length) + y * value.Length + z;
                            flatData[index] = value[x, y, z];
                        }
                    }
                }

                writer.WriteStartObject();

                writer.WriteNumber("Width", value.Width);
                writer.WriteNumber("Height", value.Height);
                writer.WriteNumber("Length", value.Length);

                writer.WritePropertyName("Data");
                JsonSerializer.Serialize(writer, flatData, options);

                writer.WriteEndObject();
            }
        }
    }
}

#endif