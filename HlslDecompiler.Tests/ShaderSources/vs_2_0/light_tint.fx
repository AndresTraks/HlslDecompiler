float4x4 wvp;
float4x4 w;
float3 eye;
float3 lightDir;
float4 fogParams;

struct VS_IN
{
	float4 position : POSITION;
	float4 normal : NORMAL;
};

struct VS_OUT
{
	float4 color : COLOR;
	float fog : FOG;
	float4 position : POSITION;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float t0 = (fogParams.y - length(mul(i.position, (float4x3)w) - eye)) * fogParams.z;
	float3 t1 = normalize(mul(i.normal.xyz, (float3x3)w));
	o.color = saturate(dot(t1, -lightDir)) * float4(1, 0.899999976, 0.800000012, 1);
	o.fog = saturate(t0);
	o.position = mul(i.position, wvp);

	return o;
}
