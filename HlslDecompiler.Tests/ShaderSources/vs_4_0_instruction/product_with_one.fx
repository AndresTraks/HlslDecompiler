struct VS_IN
{
	int3 a : A;
	float4 w : W;
	float4 c : C;
	float2 b : B;
};

struct VS_OUT
{
	float4 sv_position : SV_Position;
	float4 m : M;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float4 r0;
	r0.xyz = (float3)i.a.xyz;
	r0.w = 1;
	o.sv_position = r0 * i.w + i.c;
	r0.xz = i.b.xy;
	r0.yw = float2(1, 1);
	o.m = r0 * i.w + i.c;

	return o;
}
