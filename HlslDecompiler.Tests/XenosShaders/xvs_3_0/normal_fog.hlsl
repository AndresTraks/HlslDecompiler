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

	float3 t0 = mul(i.normal, (float3x3)w);
	float3 t1 = mul(i.position, w) - eye;
	o.texcoord = t0 * rsqrt(abs(dot(t0.zxy, t0.zxy)));
	o.fog = saturate(0.00999999978 * sqrt(abs(dot(t1, t1))));
	o.position = mul(i.position, wvp);

	return o;
}
