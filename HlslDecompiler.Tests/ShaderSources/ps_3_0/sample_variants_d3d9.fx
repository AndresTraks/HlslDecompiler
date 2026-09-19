float4 k;
sampler2D tex;

struct PS_IN
{
	float4 texcoord : TEXCOORD;
	float2 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : COLOR
{
	float4 t0 = tex2Dbias(tex, float4(i.texcoord1, 0, k.x));
	float4 t1 = tex2Dproj(tex, i.texcoord);
	float4 t2 = tex2Dgrad(tex, i.texcoord1, ddx(i.texcoord1) * k.y, ddy(i.texcoord1) * k.z);
	return t1 * k.w + t0 + t2;
}
