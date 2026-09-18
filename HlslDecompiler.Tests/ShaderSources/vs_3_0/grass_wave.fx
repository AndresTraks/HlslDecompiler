float4 wind : register(c4);
float4x4 worldViewProjection;

struct VS_IN
{
	float4 position : POSITION;
	float4 texcoord : TEXCOORD;
	float4 texcoord1 : TEXCOORD1;
};

struct VS_OUT
{
	float4 position : POSITION;
	float2 texcoord : TEXCOORD;
	float fog : FOG;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float t0 = sin(i.texcoord1.x + wind.w) * cos(0.5 * (i.texcoord1.x + wind.w)) * wind.z * i.texcoord.y;
	float t1 = i.position.x + i.texcoord1.y + wind.x * t0;
	float t2 = i.position.z + i.texcoord1.z + wind.y * t0;
	o.position = mul(float4(t1, i.position.y, t2, i.position.w), worldViewProjection);
	o.texcoord = i.texcoord.xy;
	o.fog = saturate(sqrt(t1 * t1 + i.position.y * i.position.y + t2 * t2) * -i.texcoord1.w + 1);

	return o;
}
