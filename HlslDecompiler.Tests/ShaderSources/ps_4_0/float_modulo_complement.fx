cbuffer Overlay : register(b0)
{
	float2 cellSize;
	float lineWidth;
};

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float t0 = fmod(abs(texcoord.y), cellSize.y);
	float t1 = fmod(abs(texcoord.x), cellSize.x);
	float t2 = saturate(min(min(cellSize.y - t0, t0), min(cellSize.x - t1, t1)) / lineWidth);
	return 1 - t2;
}
