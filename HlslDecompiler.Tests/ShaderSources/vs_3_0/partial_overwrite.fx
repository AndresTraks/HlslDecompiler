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
	float t0 = 10 * tex2Dlod(height, float4(0.00999999978 * lerp(i.position.xz, i.texcoord.xz, morph), 0, 0)).x + lerp(i.position.y, i.texcoord.y, morph);
	return mul(float4(lerp(i.position.x, i.texcoord.x, morph), t0, lerp(i.position.zw, i.texcoord.zw, morph)), wvp);
}
