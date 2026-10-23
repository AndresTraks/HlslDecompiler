cbuffer C : register(b0)
{
	float4x4 invViewProj;
	float4x4 decalInv;
	uint tile;
	uint tilesPerRow;
	float fadeAngle;
};

SamplerState s;
Texture2D<float> depth;
Texture2D atlas;

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
	float3 normal : NORMAL;
};

float4 main(PS_IN i) : SV_Target
{
	float2 t3 = float2(2, -2) * i.texcoord + float2(-1, 1);
	int3 t4 = int3((int2)i.sv_position.xy, 0);
	float t5 = dot(transpose(invViewProj)[3], float4(t3, depth.Load(t4).x, 1));
	float t6 = t5 / t5;
	float3 t0 = mul(float4(t3, depth.Load(t4).x, 1), (float4x3)invViewProj) / t5;
	float t1 = dot(transpose(decalInv)[0], float4(t0, t6));
	float t2 = dot(transpose(decalInv)[2], float4(t0, t6));
	bool3 t7 = bool3(0.5 - abs(t1) < 0, 0.5 - abs(dot(transpose(decalInv)[1], float4(t0, t6))) < 0, 0.5 - abs(t2) < 0);
	if (t7.z || t7.y || t7.x) {
		discard;
	}
	float2 t8 = (float2(t1 + (float)(tile % tilesPerRow), t2 + (float)(tile / tilesPerRow)) + 0.5) / (float2)tilesPerRow;
	float3 t9 = normalize(cross(ddy_coarse(t0), ddx_coarse(t0)));
	return float4(atlas.Sample(s, t8).xyz, saturate((dot(t9, i.normal) - fadeAngle) / (1 - fadeAngle)) * atlas.Sample(s, t8).w);
}
