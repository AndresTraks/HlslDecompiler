struct PS_IN
{
	noperspective centroid float2 texcoord : TEXCOORD;
	noperspective sample float2 texcoord1 : TEXCOORD1;
	centroid float texcoord2 : TEXCOORD2;
};

float4 main(PS_IN i) : SV_Target
{
	return float4(i.texcoord, i.texcoord1) * i.texcoord2;
}
