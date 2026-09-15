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
	return sceneTex.Load(int3((int2)i.sv_position.xy, 0)) * (frac(8 * (i.sv_position.x / resolution.x)) < 0.5 ? 1 : 0) + float4(depthTex.Load(int3((int)i.sv_position.x + 1, (int)i.sv_position.y, 0)).x, i.sv_position.xy / resolution, i.texcoord.x);
}
