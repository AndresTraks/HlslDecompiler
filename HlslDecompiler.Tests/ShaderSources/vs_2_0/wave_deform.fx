float4 wave : register(c4);
float4x4 worldViewProjection;

struct VS_IN
{
	float4 position : POSITION;
	float4 texcoord : TEXCOORD;
};

struct VS_OUT
{
	float4 position : POSITION;
	float2 texcoord : TEXCOORD;
	float texcoord1 : TEXCOORD1;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float t0 = sin(i.position.x * wave.x + wave.y);
	float t1 = t0 * wave.z + i.position.y;
	o.position = mul(float4(i.position.x, t1, i.position.zw), worldViewProjection);
	o.texcoord = i.texcoord.xy;
	o.texcoord1 = t0 * wave.z * wave.w;

	return o;
}
