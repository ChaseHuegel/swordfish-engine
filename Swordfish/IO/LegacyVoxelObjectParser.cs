using Swordfish.Bricks;
using Swordfish.Library.IO;

namespace Swordfish.IO;

internal class LegacyVoxelObjectParser : IFileParser<BrickGrid>
{
    public string[] SupportedExtensions { get; } =
    [
        ".svo",
    ];

    object IFileParser.Parse(PathInfo file) => Parse(file);
    public BrickGrid Parse(PathInfo file)
    {
        using Stream stream = file.Open();
        using StreamReader reader = new(stream);
        return ParseFromReader(reader);
    }

    private static BrickGrid ParseFromReader(StreamReader reader)
    {
        string name = reader.ReadLine()!;

        string[] parts = reader.ReadLine()!.Split(',');
        int chunksX = int.Parse(parts[0]);
        int chunksY = int.Parse(parts[1]);
        int chunksZ = int.Parse(parts[2]);

        BrickGrid brickGrid = new(16);

        while (reader.EndOfStream == false)
        {
            Brick brick = new(0);
            int x = 0, y = 0, z = 0;

            string entry = reader.ReadLine()!;
            string[] sections = entry.Split('/');

            for (var i = 0; i < sections.Length; i++)
            {
                string[] section = sections[i].Split(':');
                string tag = section[0];
                string value = section[1];

                switch (tag)
                {
                    case "v":
                        if (value.Contains("SLOPE"))
                        {
                            brick = new Brick(2);
                        }
                        else if (value.Equals("THRUSTER_ROCKET"))
                        {
                            brick = new Brick(3);
                        }
                        else if (value.Equals("THRUSTER_ROCKET_INTERNAL"))
                        {
                            brick = new Brick(4);
                        }
                        else
                        {
                            brick = new Brick(1);
                        }

                        brick.Name = value.ToLower();
                        break;

                    case "p":
                        parts = value.Split(',');
                        x = int.Parse(parts[0]);
                        y = int.Parse(parts[1]);
                        z = int.Parse(parts[2]);
                        break;

                    case "r":
                        break;

                    case "o":
                        break;
                }
            }

            if (brick.ID > 0 && x >= 0 && y >= 0 && z >= 0)
            {
                brickGrid.Set(x, y, z, brick);
            }
        }

        return brickGrid;
    }
}
