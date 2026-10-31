struct PS_IN
{
	centroid float4 texcoord : TEXCOORD;
	centroid float4 color : COLOR;
};

float4 main(PS_IN i) : COLOR
{
	return i.texcoord * i.color;
}
