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

	float4 r0;
	r0.xy = i.texcoord.xy;
	r0.zw = float2(0, 1);
	o.sv_target = r0 * tint;
	r0.x = dot(i.normal.xyz, i.normal.xyz);
	r0.x = 1 / sqrt(r0.x);
	r0.xyz = r0.xxx * i.normal.xyz;
	o.sv_target1.xyz = r0.xyz * float3(0.5, 0.5, 0.5) + float3(0.5, 0.5, 0.5);
	o.sv_target1.w = 1;
	o.sv_depth = i.sv_position.z * 0.5;

	return o;
}
