using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace ovkdesktop.Converters
{
    public class BoolAsIntJsonConverter : JsonConverter<int>
    {
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Number:
                    return reader.GetInt32();

                case JsonTokenType.True:
                    return 1;

                case JsonTokenType.False:
                    return 0;

                case JsonTokenType.String:
                    if (int.TryParse(reader.GetString(), out int value))
                    {
                        return value;
                    }
                    break;
            }

            // Return 0 on error
            return 0;
        }

        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value);
        }
    }
}
