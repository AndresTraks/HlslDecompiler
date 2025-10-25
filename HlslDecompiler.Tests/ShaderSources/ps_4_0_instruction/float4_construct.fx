struct PS_OUT
{
	float4 sv_target : SV_Target;
	float4 sv_target1 : SV_Target1;
	float4 sv_target2 : SV_Target2;
	float4 sv_target3 : SV_Target3;
};

PS_OUT main(float4 texcoord : TEXCOORD)
{
	PS_OUT o;

	o.sv_target = texcoord;
	o.sv_target1.xyz = texcoord.xyz;
	o.sv_target1.w = 0;
	o.sv_target2.xy = texcoord.xy;
	o.sv_target2.zw = float2(0, 0);
	o.sv_target3.x = texcoord.x;
	o.sv_target3.yzw = float3(0, 0, 1);

	return o;
}
