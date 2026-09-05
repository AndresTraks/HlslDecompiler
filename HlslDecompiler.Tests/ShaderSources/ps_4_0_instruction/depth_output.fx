float4 a;
float b;

struct PS_OUT
{
	float4 sv_target : SV_Target;
	float sv_depth : SV_Depth;
};

PS_OUT main(float4 texcoord : TEXCOORD)
{
	PS_OUT o;

	o.sv_target = texcoord * a;
	o.sv_depth = b;

	return o;
}
