sampler2D heightMap;
float4x4 wvp;

struct VS_IN
{
	float4 position : POSITION;
	float4 texcoord : TEXCOORD;
};

float4 main(VS_IN i) : POSITION
{
	float t0 = tex2Dlod(heightMap, float4(i.texcoord.xy, 0, 0)).x;
	return mul(float4(i.position.x, t0 + i.position.y, i.position.zw), wvp);
}
