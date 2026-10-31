struct PS_IN
{
	float2 texcoord1 : TEXCOORD1;
	float3 texcoord3 : TEXCOORD3;
	centroid float4 color : COLOR;
};

float4 main(PS_IN i) : COLOR
{
	return i.color * i.texcoord1.x + float4(i.texcoord3, i.texcoord1.y);
}
