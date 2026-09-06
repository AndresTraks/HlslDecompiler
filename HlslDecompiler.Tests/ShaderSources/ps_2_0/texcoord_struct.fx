struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float4 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : COLOR
{
	return float4(i.texcoord * i.texcoord1.xx, i.texcoord1.zw * i.texcoord1.xx);
}
