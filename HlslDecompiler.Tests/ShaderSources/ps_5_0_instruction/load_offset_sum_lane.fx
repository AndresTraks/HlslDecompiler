cbuffer c : register(b0)
{
	int slice;
	int2 off;
};

Texture2DArray t;

float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	float4 o;

	int4 r0;
	int4 r1;
	float4 r2;
	r0.z = slice + 1;
	r0.w = 1;
	r1.xy = (int2)sv_position.xy;
	r0.xy = r1.xy + off.xy;
	r0 = asint(t.Load(r0));
	r1.z = slice;
	r1.w = 0;
	r2 = t.Load(r1, int2(1, -1));
	r1 = asint(t.Load(r1));
	r0 = asint(asfloat(r0) * float4(0.5, 0.5, 0.5, 0.5) + r2);
	o = -(asfloat(r1)) + asfloat(r0);

	return o;
}
