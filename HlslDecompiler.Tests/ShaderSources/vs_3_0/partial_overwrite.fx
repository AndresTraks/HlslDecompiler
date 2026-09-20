sampler2D height;
float morph : register(c4);
float4x4 wvp;

struct VS_IN
{
	float4 position : POSITION;
	float4 texcoord : TEXCOORD;
};

float4 main(VS_IN i) : POSITION
{
	float4 t0 = lerp(i.position, i.texcoord, morph);
	float t1 = 10 * tex2Dlod(height, float4(0.00999999978 * t0.xz, 0, 0)).x + t0.y;
	return mul(float4(t0.x, t1, t0.zw), wvp);
}
