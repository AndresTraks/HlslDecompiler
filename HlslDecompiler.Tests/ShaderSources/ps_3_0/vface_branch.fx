float4 p;
sampler2D t;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float vface : VFACE;
};

float4 main(PS_IN i) : COLOR
{
	if (i.vface > 0) {
		return tex2Dlod(t, float4(i.texcoord, 0, p.x));
	} else {
		return tex2Dgrad(t, 2 * i.texcoord, ddx(i.texcoord), ddy(i.texcoord)).zyxw;
	}
}
