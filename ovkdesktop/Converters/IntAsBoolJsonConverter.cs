using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace ovkdesktop.Converters
{
    public class IntAsBoolJsonConverter : JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                // If the API sends a number (1 or 0)
                case JsonTokenType.Number:
                    return reader.GetInt32() != 0;

                // If the API sends a boolean value (true or false)
                case JsonTokenType.True:
                    return true;

                case JsonTokenType.False:
                    return false;
            }

            // In case of error, assume the value is false
            return false;
        }

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        {
            writer.WriteBooleanValue(value);
        }
    }
}
