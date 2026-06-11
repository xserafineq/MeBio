using System.Formats.Cbor;

namespace MeBio.Services;

internal static class SourceAfisTemplateMinutiae
{
    internal sealed record MinutiaPoint(float X, float Y, float DirectionRadians, bool IsEnding);

    internal sealed record ParsedTemplate(ushort Width, ushort Height, IReadOnlyList<MinutiaPoint> Points);

    internal static ParsedTemplate? TryParse(byte[] templateBytes)
    {
        if (templateBytes.Length == 0)
            return null;

        try
        {
            var reader = new CborReader(templateBytes);
            if (reader.PeekState() != CborReaderState.StartMap)
                return null;

            ushort width = 0;
            ushort height = 0;
            ushort[]? positionsX = null;
            ushort[]? positionsY = null;
            float[]? directions = null;
            string? types = null;

            var mapLength = reader.ReadStartMap() ?? 0;
            for (var i = 0; i < mapLength; i++)
            {
                var key = reader.ReadTextString();
                switch (key)
                {
                    case "width":
                        width = (ushort)reader.ReadUInt32();
                        break;
                    case "height":
                        height = (ushort)reader.ReadUInt32();
                        break;
                    case "positionsX":
                        positionsX = ReadUInt16Array(reader);
                        break;
                    case "positionsY":
                        positionsY = ReadUInt16Array(reader);
                        break;
                    case "directions":
                        directions = ReadSingleArray(reader);
                        break;
                    case "types":
                        types = reader.ReadTextString();
                        break;
                    default:
                        reader.SkipValue();
                        break;
                }
            }

            reader.ReadEndMap();

            if (width == 0 || height == 0 || positionsX is null || positionsY is null || types is null)
                return null;

            var count = Math.Min(positionsX.Length, Math.Min(positionsY.Length, types.Length));
            if (count == 0)
                return null;

            var points = new List<MinutiaPoint>(count);
            for (var j = 0; j < count; j++)
            {
                var direction = directions is not null && j < directions.Length ? directions[j] : 0f;
                points.Add(new MinutiaPoint(
                    positionsX[j],
                    positionsY[j],
                    direction,
                    types[j] != 'B'));
            }

            return new ParsedTemplate(width, height, points);
        }
        catch
        {
            return null;
        }
    }

    private static ushort[] ReadUInt16Array(CborReader reader)
    {
        var length = reader.ReadStartArray() ?? 0;
        var values = new ushort[length];
        for (var i = 0; i < length; i++)
            values[i] = (ushort)reader.ReadUInt32();

        reader.ReadEndArray();
        return values;
    }

    private static float[] ReadSingleArray(CborReader reader)
    {
        var length = reader.ReadStartArray() ?? 0;
        var values = new float[length];
        for (var i = 0; i < length; i++)
            values[i] = (float)reader.ReadDouble();

        reader.ReadEndArray();
        return values;
    }
}
