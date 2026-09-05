sampler2D heightMap;
float4x4 wvp;

struct VS_IN
{
	float4 position : POSITION;
	float4 texcoord : TEXCOORD;
};

float4 main(VS_IN i) : POSITION
{
	return mul(float4(i.position.x, tex2Dlod(heightMap, float4(i.texcoord.xy, 0, 0)).x + i.position.y, i.position.zw), wvp);
}
