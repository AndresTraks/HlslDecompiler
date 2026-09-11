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
	return mul(float4(morph * (i.texcoord.x - i.position.x) + i.position.x, 10 * tex2Dlod(height, float4(0.00999999978 * (morph * (i.texcoord.xz - i.position.xz) + i.position.xz), 0, 0)).x + morph * (i.texcoord.y - i.position.y) + i.position.y, morph * (i.texcoord.zw - i.position.zw) + i.position.zw), wvp);
}
