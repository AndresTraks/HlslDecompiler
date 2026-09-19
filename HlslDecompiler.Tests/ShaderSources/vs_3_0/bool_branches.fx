float4 offset : register(c6);
float scale : register(c7);
bool useOffset : register(c4);
bool useScale : register(c5);
float4x4 worldViewProj;

struct VS_IN
{
	float4 position : POSITION;
	float4 color : COLOR;
};

struct VS_OUT
{
	float4 position : POSITION;
	float4 color : COLOR;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float3 t0 = useOffset * offset.xyz + i.position.xyz;
	float3 t1 = lerp(t0, t0 * scale, useScale);
	o.position = mul(float4(t1, useOffset * offset.w + i.position.w), worldViewProj);
	o.color = lerp(i.color.wzyx, i.color, useOffset);

	return o;
}
