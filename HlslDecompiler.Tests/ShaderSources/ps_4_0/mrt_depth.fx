float4 tint;

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	float3 normal : NORMAL;
	float2 texcoord : TEXCOORD;
};

struct PS_OUT
{
	float4 sv_target : SV_Target;
	float4 sv_target1 : SV_Target1;
	float sv_depth : SV_Depth;
};

PS_OUT main(PS_IN i)
{
	PS_OUT o;

	o.sv_target = float4(i.texcoord * tint.xy, 0, tint.w);
	o.sv_depth = 0.5 * i.sv_position.z;
	o.sv_target1 = float4(0.5 * normalize(i.normal) + 0.5, 1);

	return o;
}
