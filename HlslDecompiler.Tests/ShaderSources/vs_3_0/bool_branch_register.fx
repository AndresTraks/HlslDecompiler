float3 eye : register(c5);
sampler2D heightMap;
float4 morph : register(c4);
bool useMorph : register(b0);
float4x4 viewProj;

struct VS_OUT
{
	float4 position : POSITION;
	float2 texcoord : TEXCOORD;
	float texcoord1 : TEXCOORD1;
};

VS_OUT main(float4 position : POSITION)
{
	VS_OUT o;

	float2 t0 = morph.z * position.xy;
	float t1 = tex2Dlod(heightMap, float4(t0, 0, 0)).x;
	float t2 = t1 * morph.w;
	float2 t3;
	if (useMorph) {
		float t4 = saturate((length(float3(position.x, t2, position.y) - eye) - morph.x) / morph.y);
		t3 = lerp(position.xy, 2 * floor(0.5 * position.xy), t4);
	} else {
		t3 = position.xy;
	}
	o.position = mul(float4(t3.x, t2, t3.y, 1), viewProj);
	o.texcoord = t0;
	o.texcoord1 = t2;

	return o;
}
