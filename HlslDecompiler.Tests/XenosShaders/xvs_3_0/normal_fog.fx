float3 eye : register(c7);
float4x3 w : register(c4);
float4x4 wvp;

struct VS_IN
{
	float4 position : POSITION;
	float3 normal : NORMAL;
};

struct VS_OUT
{
	float3 texcoord : TEXCOORD;
	float fog : FOG;
	float4 position : POSITION;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	o.texcoord = normalize(mul(i.normal, (float3x3)w));
	o.fog = saturate(distance(mul(i.position, w), eye) * 0.01);
	o.position = mul(i.position, wvp);

	return o;
}
