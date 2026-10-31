struct PS_IN
{
	float4 texcoord : TEXCOORD;
	float4 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : COLOR
{
	return -((i.texcoord1 < i.texcoord) ? 1 : 0) >= 0 ? i.texcoord1 : i.texcoord;
}
