float4 k;
sampler2D tex;

struct PS_IN
{
	float4 texcoord : TEXCOORD;
	float2 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : COLOR
{
	return tex2Dproj(tex, i.texcoord) * k.w + tex2Dbias(tex, float4(i.texcoord1, 0, k.x)) + tex2Dgrad(tex, i.texcoord1, ddx(i.texcoord1) * k.y, ddy(i.texcoord1) * k.z);
}
