struct PS_IN
{
	noperspective centroid float2 texcoord : TEXCOORD;
	noperspective sample float2 texcoord1 : TEXCOORD1;
	centroid float texcoord2 : TEXCOORD2;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	r0.xy = i.texcoord.xy;
	r0.zw = i.texcoord1.xy;
	o = r0 * i.texcoord2.x;

	return o;
}
