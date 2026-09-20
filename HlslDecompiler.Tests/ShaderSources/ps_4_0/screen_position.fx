float2 resolution;

Texture2D sceneTex;
Texture2D depthTex;

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	float2 t0 = i.sv_position.xy / resolution;
	float t1 = frac(8 * t0.x) < 0.5 ? 1.0 : 0;
	int2 t2 = (int2)i.sv_position.xy;
	return sceneTex.Load(int3(t2, 0)) * t1 + float4(depthTex.Load(int3(t2 + int2(1, 0), 0)).x, t0, i.texcoord.x);
}
