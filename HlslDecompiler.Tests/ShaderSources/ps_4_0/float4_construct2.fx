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

	o.sv_target = float4(i.texcoord.x, 0, 1, i.texcoord.y);
	o.sv_target1 = float4(0, 1, i.texcoord);
	o.sv_target2 = float4(0, 1, 2, i.texcoord.x);
	o.sv_target3 = float4(i.texcoord, i.texcoord1.zw);

	return o;
}
