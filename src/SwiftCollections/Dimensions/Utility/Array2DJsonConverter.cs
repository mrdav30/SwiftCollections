#if NET8_0_OR_GREATER

using System;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace SwiftCollections.Dimensions
{
    public class Array2DJsonConverter : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            // Walk base types to find Array2D<T>
            while (typeToConvert != null && typeToConvert != typeof(object))
            {
                if (typeToConvert.IsGenericType &&
                    typeToConvert.GetGenericTypeDefinition() == typeof(Array2D<>))
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
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(Array2D<>))
                {
                    Type itemType = current.GetGenericArguments()[0];
                    Type bridgeConverterType = typeof(Array2DJsonConverterBridge<,>).MakeGenericType(typeToConvert, itemType);
                    return (JsonConverter)Activator.CreateInstance(bridgeConverterType);
                }

                current = current.BaseType;
            }

            throw new InvalidOperationException("Array2DJsonConverterFactory: Unable to create converter for " + typeToConvert);
        }

        // This bridge allows us to handle subclasses (BoolArray2D, ShortArray2D, etc.)
        private class Array2DJsonConverterBridge<TTarget, T> : JsonConverter<TTarget> where TTarget : Array2D<T>
        {
            public override TTarget Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var doc = JsonDocument.ParseValue(ref reader);
                int width = doc.RootElement.GetProperty("Width").GetInt32();
                int height = doc.RootElement.GetProperty("Height").GetInt32();

                T[] data = JsonSerializer.Deserialize<T[]>(
                    doc.RootElement.GetProperty("Data").GetRawText(), options);

                // Must use Activator to create TTarget (BoolArray2D, ShortArray2D, etc.)
                var result = (TTarget)Activator.CreateInstance(typeof(TTarget), width, height);

                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        result[x, y] = data[x * height + y];
                    }
                }

                return result;
            }

            public override void Write(Utf8JsonWriter writer, TTarget value, JsonSerializerOptions options)
            {
                T[] flatData = new T[value.Width * value.Height];
                for (int x = 0; x < value.Width; x++)
                {
                    for (int y = 0; y < value.Height; y++)
                    {
                        flatData[x * value.Height + y] = value[x, y];
                    }
                }

                writer.WriteStartObject();

                writer.WriteNumber("Width", value.Width);
                writer.WriteNumber("Height", value.Height);

                writer.WritePropertyName("Data");
                JsonSerializer.Serialize(writer, flatData, options);

                writer.WriteEndObject();
            }
        }
    }
}

#endif