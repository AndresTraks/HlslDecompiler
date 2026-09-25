cbuffer Params : register(b0)
{
	float4 tint;
	float cutoff;
};

struct PS_OUT
{
	float4 sv_target : SV_Target;
	float4 sv_target1 : SV_Target1;
};

PS_OUT main(float2 texcoord : TEXCOORD)
{
	PS_OUT o;

	int r0;
	r0 = (texcoord.x < cutoff) ? -1 : 0;
	if (r0.x != 0) {
		o.sv_target = float4(1, 0, 0, 1);
		o.sv_target1 = tint;
		return o;
	}
	o.sv_target = texcoord.y * tint;
	o.sv_target1.xy = texcoord.xy;
	o.sv_target1.zw = float2(0, 1);

	return o;
}
