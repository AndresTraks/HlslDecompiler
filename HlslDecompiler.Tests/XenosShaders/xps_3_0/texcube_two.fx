samplerCUBE s;
samplerCUBE t;

struct PS_IN
{
	float3 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : COLOR
{
	float4 t1 = texCUBE(s, i.texcoord - 2 * (i.texcoord.x + i.texcoord1.y) * i.texcoord1);
	float4 t2 = texCUBE(t, 0.75 * i.texcoord - (0.75 * dot(i.texcoord, i.texcoord1) + dot(i.texcoord, i.texcoord)) * i.texcoord1);
	return t1 * t2;
}
