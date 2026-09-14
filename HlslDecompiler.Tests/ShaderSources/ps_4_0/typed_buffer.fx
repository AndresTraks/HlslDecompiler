Buffer<float4> colours;
Buffer<uint> indices;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return colours.Load(indices.Load((int)(16 * texcoord.x)).x) * texcoord.y;
}
