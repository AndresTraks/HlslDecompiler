StructuredBuffer<int> buf : register(t0);

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	float4 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int2 r0;
	r0.x = (int)i.sv_position.y;
	r0.x = buf[r0.x];
	r0.x = r0.x & 3;
	r0.y = (r0.x != 0) ? 0 : 1065353216;
	r0.x = asint(dot(i.sv_position, i.texcoord));
	o = asfloat(r0.xxyy);

	return o;
}
