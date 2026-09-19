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
	float t0 = morph * (i.texcoord.y - i.position.y) + i.position.y + 10 * tex2Dlod(height, float4(0.00999999978 * (morph * (i.texcoord.xz - i.position.xz) + i.position.xz), 0, 0)).x;
	return mul(float4(morph * (i.texcoord.x - i.position.x) + i.position.x, t0, morph * (i.texcoord.zw - i.position.zw) + i.position.zw), wvp);
}
