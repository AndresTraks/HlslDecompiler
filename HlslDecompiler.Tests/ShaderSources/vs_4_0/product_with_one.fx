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

	o.sv_position = float4((float3)i.a, 1) * i.w + i.c;
	o.m = float4(i.b.x, 1, i.b.y, 1) * i.w + i.c;

	return o;
}
