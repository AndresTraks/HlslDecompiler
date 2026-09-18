float3 attenuation : register(c5);
float4 light : register(c4);
float4x4 worldViewProj;

struct VS_IN
{
	float4 position : POSITION;
	float4 normal : NORMAL;
};

struct VS_OUT
{
	float4 color : COLOR;
	float4 position : POSITION;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float3 t0 = light.xyz - i.position.xyz;
	float t1 = rcp(length(t0) * length(t0) * length(t0) * attenuation.x + length(t0) * length(t0) * attenuation.y + length(t0) * attenuation.z);
	o.position = mul(i.position, worldViewProj);
	o.color = float4(sign(dot(normalize(t0), i.normal.xyz)) * t1, exp2(log2(saturate(t1)) * light.w), length(t0), 1);

	return o;
}
