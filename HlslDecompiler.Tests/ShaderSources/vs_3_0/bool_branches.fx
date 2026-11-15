float4x4 worldViewProj;
bool useOffset;
bool useScale;
float4 offset;
float scale;

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
