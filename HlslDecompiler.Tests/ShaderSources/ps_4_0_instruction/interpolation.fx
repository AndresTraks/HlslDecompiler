float4 a;

struct PS_IN
{
	nointerpolation float4 texcoord : TEXCOORD;
	centroid float4 texcoord1 : TEXCOORD1;
	noperspective float4 texcoord2 : TEXCOORD2;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	r0 = i.texcoord * a + i.texcoord1;
	o = r0 + i.texcoord2;

	return o;
}
