struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float4 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : COLOR
{
	float4 o;

	float4 r0;
	r0.xy = i.texcoord.xy;
	r0.zw = i.texcoord1.zw;
	r0 = r0 * i.texcoord1.x;
	o = r0;

	return o;
}
