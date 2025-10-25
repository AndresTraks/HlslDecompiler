struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float4 texcoord1 : TEXCOORD1;
};

struct PS_OUT
{
	float4 sv_target : SV_Target;
	float4 sv_target1 : SV_Target1;
	float4 sv_target2 : SV_Target2;
	float4 sv_target3 : SV_Target3;
};

PS_OUT main(PS_IN i)
{
	PS_OUT o;

	float4 r0;
	r0.zw = i.texcoord.xy;
	r0.xy = float2(0, 1);
	o.sv_target = r0.zxyw;
	o.sv_target1 = r0;
	o.sv_target2.xyz = float3(0, 1, 2);
	o.sv_target2.w = i.texcoord.x;
	o.sv_target3.xy = i.texcoord.xy;
	o.sv_target3.zw = i.texcoord1.zw;

	return o;
}
