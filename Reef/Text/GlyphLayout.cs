namespace Reef.Text;

public readonly struct GlyphLayout(IntRect bbox, IntRect uv)
{
    public readonly IntRect BBOX = bbox;
    public readonly IntRect UV = uv;
}